import argparse
from contextlib import ExitStack
from email.parser import BytesParser
from email.policy import default
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys
import tarfile
import tempfile
from urllib.parse import urlparse


DEFAULT_PROVENANCE = (
    Path(__file__).resolve().parents[1] / "packaging" / "ifc-runtime" / "provenance.json"
)
DEFAULT_SOURCE_CACHE = Path(__file__).resolve().parents[3] / "work" / "ifc-source-cache"
LEGAL_NAME = re.compile(r"licen[cs]e|copying|copyright|notice|authors", re.IGNORECASE)
SHA256 = re.compile(r"[0-9a-f]{64}")


def normalized_name(name):
    return re.sub(r"[-_.]+", "-", name).lower()


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def inside(root, relative):
    portable = PurePosixPath(relative)
    if (
        not relative
        or "\\" in relative
        or ":" in relative
        or portable.is_absolute()
        or any(part in (".", "..") for part in relative.split("/"))
    ):
        raise ValueError(f"Unsafe evidence path {relative!r}")
    target = root.joinpath(*portable.parts)
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError(f"Unsafe evidence path {relative!r}")
    return target


def validate_sources(component):
    if not component.get("sources"):
        raise ValueError(f"Missing source provenance for {component['name']}")
    for source in component["sources"]:
        parsed = urlparse(source.get("url", ""))
        if (
            parsed.scheme != "https"
            or not parsed.netloc
            or parsed.username
            or parsed.password
            or not SHA256.fullmatch(source.get("sha256", ""))
            or not isinstance(source.get("size"), int)
            or source["size"] <= 0
            or source.get("status") not in ("identified", "acquired")
        ):
            raise ValueError(f"Invalid source provenance for {component['name']}")


def verify_fingerprints(component, root):
    for fingerprint in component.get("fingerprints", []):
        path = inside(root, fingerprint["path"])
        if not path.is_file() or digest(path) != fingerprint["sha256"]:
            raise ValueError(
                f"Provenance fingerprint mismatch for {component['name']} "
                f"at {fingerprint['path']}"
            )


def collect(python_base, site_packages, output, provenance_path, source_cache):
    python_base = python_base.resolve(strict=True)
    site_packages = site_packages.resolve(strict=True)
    output = output.resolve()
    provenance_path = provenance_path.resolve(strict=True)
    for input_root in (python_base, site_packages):
        if output.is_relative_to(input_root) or input_root.is_relative_to(output):
            raise ValueError("Output and evidence inputs overlap")
    if provenance_path.is_relative_to(output):
        raise ValueError("Output and provenance input overlap")
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError("Refusing nonempty output directory")
    catalogue = json.loads(provenance_path.read_text(encoding="utf-8-sig"))
    if catalogue.get("schemaVersion") != 1:
        raise ValueError("Unsupported provenance schemaVersion")
    source_cache = source_cache.resolve(strict=True)
    artifacts = catalogue.get("sourceArtifacts", {})
    if catalogue.get("sourceArtifactsFile"):
        artifact_path = inside(provenance_path.parent, catalogue["sourceArtifactsFile"])
        artifact_document = json.loads(artifact_path.read_text(encoding="utf-8-sig"))
        if artifact_document.get("schemaVersion") != 1:
            raise ValueError("Unsupported source artifacts schemaVersion")
        artifacts = artifact_document["artifacts"]
    for source in artifacts.values():
        validate_sources({"name": "source archive", "sources": [source]})
        if source["status"] == "acquired":
            cached = inside(source_cache, source["cacheFile"])
            if (
                not cached.is_file()
                or cached.stat().st_size != source["size"]
                or digest(cached) != source["sha256"]
            ):
                raise ValueError(f"Source archive missing or digest mismatch for {source['cacheFile']}")
    for component in catalogue["components"] + catalogue.get("nativeComponents", []):
        if "sourceIds" in component:
            component["sources"] = [artifacts[source_id] for source_id in component["sourceIds"]]
    pinned = {}
    for component in catalogue["components"]:
        name = normalized_name(component["name"])
        if name in pinned:
            raise ValueError(f"Duplicate provenance for {name}")
        validate_sources(component)
        pinned[name] = component

    pending_files = {}
    components = []

    def evidence_file(source, destination, origin, source_path, source_url=None):
        content = source.read_bytes()
        if not content.strip():
            raise ValueError(f"Empty licence or evidence file {source_path}")
        if destination in pending_files:
            raise ValueError(f"Duplicate evidence destination {destination}")
        pending_files[destination] = content
        evidence = {
            "path": destination,
            "size": len(content),
            "sha256": hashlib.sha256(content).hexdigest(),
            "origin": origin,
            "sourcePath": source_path,
        }
        if source_url:
            evidence["sourceUrl"] = source_url
        return evidence

    def supplements(component):
        files = []
        for licence in component.get("supplementalLicenses", []):
            source = inside(provenance_path.parent, licence["path"])
            if not source.is_file() or digest(source) != licence["sha256"]:
                raise ValueError(f"Upstream licence digest mismatch for {component['name']}")
            destination = f"texts/{normalized_name(component['name'])}/upstream/{licence['path']}"
            files.append(
                evidence_file(source, destination, "upstream", licence["path"], licence["url"])
            )
        with ExitStack() as stack:
            opened = {}
            for licence in component.get("archiveLicenses", []):
                source_id = licence["sourceId"]
                artifact = artifacts[source_id]
                if artifact["status"] != "acquired":
                    raise ValueError(f"Source archive must be acquired for {source_id}")
                archive_path = inside(source_cache, artifact["cacheFile"])
                member_name = licence.get("member")
                source_path = artifact["cacheFile"]
                if member_name:
                    inside(source_cache, member_name)
                    if source_id not in opened:
                        opened[source_id] = stack.enter_context(tarfile.open(archive_path))
                    member = opened[source_id].getmember(member_name)
                    if not member.isfile() or member.size > 2_000_000:
                        raise ValueError(f"Unsafe source licence member {member_name}")
                    with opened[source_id].extractfile(member) as stream:
                        content = stream.read()
                    source_path += "/" + member_name
                else:
                    content = archive_path.read_bytes()
                if not content.strip() or hashlib.sha256(content).hexdigest() != licence["sha256"]:
                    raise ValueError(f"Upstream licence digest mismatch for {component['name']}")
                destination = f"texts/{normalized_name(component['name'])}/upstream/{source_id}/{member_name or archive_path.name}"
                if destination in pending_files:
                    raise ValueError(f"Duplicate evidence destination {destination}")
                pending_files[destination] = content
                files.append(
                    {
                        "path": destination,
                        "size": len(content),
                        "sha256": licence["sha256"],
                        "origin": "upstream",
                        "sourcePath": source_path,
                        "sourceUrl": artifact["url"],
                    }
                )
        return files

    def component_record(component):
        record = {
            key: value
            for key, value in component.items()
            if key not in ("supplementalLicenses", "archiveLicenses", "fingerprints", "sourceIds")
        }
        record["licenseFiles"] = supplements(component)
        return record

    python = pinned.get("cpython")
    if python is None:
        raise ValueError("Missing CPython provenance")
    verify_fingerprints(python, python_base)
    python_licence = python_base / "LICENSE.txt"
    if not python_licence.is_file():
        raise ValueError("Missing CPython licence LICENSE.txt")
    python_record = component_record(python)
    python_record["licenseFiles"].append(
        evidence_file(
            python_licence, "texts/cpython/LICENSE.txt", "python-base", "LICENSE.txt"
        )
    )
    python_record["nativeFiles"] = []
    python_native = list(python_base.glob("*.dll")) + list(python_base.glob("*.exe"))
    python_native += list((python_base / "DLLs").glob("*.dll"))
    python_native += list((python_base / "DLLs").glob("*.pyd"))
    for path in sorted(python_native):
        relative = path.relative_to(python_base).as_posix()
        inside(python_base, relative)
        python_record["nativeFiles"].append(
            {"path": relative, "size": path.stat().st_size, "sha256": digest(path)}
        )
    components.append(python_record)

    distributions = sorted(site_packages.glob("*.dist-info"))
    if not distributions:
        raise ValueError("No wheel distributions found in site-packages")
    seen = set()
    for distribution in distributions:
        inside(site_packages, distribution.name)
        metadata_path = inside(distribution, "METADATA")
        metadata = BytesParser(policy=default).parsebytes(metadata_path.read_bytes())
        name = normalized_name(str(metadata.get("Name", "")))
        version = str(metadata.get("Version", ""))
        component = pinned.get(name)
        if component is None or version != component["version"]:
            raise ValueError(f"Missing matching provenance for {name} {version}")
        if name in seen:
            raise ValueError(f"Duplicate installed distribution {name}")
        seen.add(name)
        verify_fingerprints(component, site_packages)
        record = component_record(component)
        record["metadataLicenseExpression"] = metadata.get("License-Expression")
        record["metadataLicense"] = metadata.get("License")
        record["projectUrls"] = metadata.get_all("Project-URL", [])
        record["metadataEvidence"] = evidence_file(
            metadata_path,
            f"metadata/{name}/METADATA",
            "site-packages",
            metadata_path.relative_to(site_packages).as_posix(),
        )
        licence_paths = set()
        licence_root = distribution / "licenses"
        for declared in metadata.get_all("License-File", []):
            candidates = [inside(licence_root, declared), inside(distribution, declared)]
            actual = next((path for path in candidates if path.is_file()), None)
            if actual is None:
                raise ValueError(f"Missing declared licence {declared} for {name}")
            licence_paths.add(actual)
        for path in distribution.rglob("*"):
            if path.is_file() and (
                path.is_relative_to(licence_root) or LEGAL_NAME.search(path.name)
            ):
                inside(distribution, path.relative_to(distribution).as_posix())
                licence_paths.add(path)
        for path in sorted(licence_paths):
            relative = path.relative_to(distribution).as_posix()
            record["licenseFiles"].append(
                evidence_file(
                    path,
                    f"texts/{name}/wheel/{relative}",
                    "site-packages",
                    path.relative_to(site_packages).as_posix(),
                )
            )
        if not record["licenseFiles"]:
            raise ValueError(f"Missing licence text for {name}")
        record["nativeFiles"] = []
        for pattern in component.get("nativePatterns", []):
            for path in sorted(site_packages.glob(pattern)):
                relative = path.relative_to(site_packages).as_posix()
                inside(site_packages, relative)
                record["nativeFiles"].append(
                    {"path": relative, "size": path.stat().st_size, "sha256": digest(path)}
                )
        components.append(record)

    missing = sorted(set(pinned) - {"cpython"} - seen)
    if missing:
        raise ValueError(f"Missing pinned distributions {', '.join(missing)}")
    native_components = []
    for native in catalogue.get("nativeComponents", []):
        record = component_record(native)
        native_components.append(record)
    blockers = catalogue.get("releaseBlockers", [])
    if not blockers:
        blockers = ["Owner review of redistribution and corresponding source remains required."]
    manifest = {
        "schemaVersion": 1,
        "components": components,
        "nativeComponents": native_components,
        "sourceArtifacts": artifacts,
        "publicReleaseGate": {"status": "blocked", "blockers": blockers},
        "evidenceLimit": (
            "Collected texts, metadata and binary hashes support review. "
            "Metadata and source URLs alone do not establish redistribution compliance."
        ),
    }
    notice_lines = [
        "Offline IFC runtime third-party notices",
        "",
        manifest["evidenceLimit"],
        "Public binary release gate is blocked. See PUBLIC-RELEASE-GATE.md.",
        "",
    ]
    for component in components + native_components:
        notice_lines.append(f"{component['name']} {component.get('version') or 'version unresolved'}")
        notice_lines.append(f"Declared or audited licence {component.get('licenseExpression', 'unresolved')}")
        for source in component.get("sources", []):
            notice_lines.extend(
                [source["url"], f"sha256 {source['sha256']} bytes {source['size']}"]
            )
        for licence in component["licenseFiles"]:
            notice_lines.append(f"Licence text {licence['path']} sha256 {licence['sha256']}")
        for note in component.get("notes", []):
            notice_lines.append(note)
        notice_lines.append("")
    gate_lines = [
        "# Public binary release gate",
        "",
        "Status **blocked**.",
        "",
        "The owner must resolve and record these findings before public binary release.",
        "",
    ]
    gate_lines.extend(f"- {blocker}" for blocker in blockers)
    gate_lines.extend(
        [
            "",
            "Retain the acquired source archives and upstream build material referenced in components.json.",
            "Select and implement an appropriate source-accompanying distribution method after review.",
            "Keep Python, .py files, .pyd files and DLLs externally replaceable.",
            "See LIBRARY-REPLACEMENT.md for replacement and manifest regeneration.",
            "",
        ]
    )
    pending_files["components.json"] = (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
    pending_files["THIRD-PARTY-NOTICES.txt"] = ("\n".join(notice_lines) + "\n").encode("utf-8")
    pending_files["PUBLIC-RELEASE-GATE.md"] = "\n".join(gate_lines).encode("utf-8")
    pending_files["LIBRARY-REPLACEMENT.md"] = (
        DEFAULT_PROVENANCE.parent / "LIBRARY-REPLACEMENT.md"
    ).read_bytes()
    for relative in pending_files:
        inside(output, relative)
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".ifc-licenses-", dir=output.parent) as temporary:
        stage = Path(temporary).resolve()
        if stage.parent != output.parent:
            raise ValueError("Unsafe licence staging directory")
        for relative, content in sorted(pending_files.items()):
            destination = inside(stage, relative)
            destination.parent.mkdir(parents=True, exist_ok=True)
            with destination.open("xb") as stream:
                stream.write(content)
        if output.exists():
            output.rmdir()
        stage.rename(output)
    return manifest


def main():
    parser = argparse.ArgumentParser(description="Collect offline IFC runtime licence evidence")
    parser.add_argument("--python-base", type=Path, required=True)
    parser.add_argument("--site-packages", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--provenance", type=Path, default=DEFAULT_PROVENANCE)
    parser.add_argument("--source-cache", type=Path, default=DEFAULT_SOURCE_CACHE)
    arguments = parser.parse_args()
    try:
        manifest = collect(
            arguments.python_base,
            arguments.site_packages,
            arguments.output,
            arguments.provenance,
            arguments.source_cache,
        )
    except (OSError, ValueError, KeyError, TypeError, tarfile.TarError) as error:
        print(f"Licence collection failed. {error}", file=sys.stderr)
        return 1
    print(
        f"Collected {len(manifest['components'])} components. "
        f"Public release gate {manifest['publicReleaseGate']['status']}."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
