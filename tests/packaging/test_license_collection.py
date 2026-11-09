import hashlib
import io
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch


REPOSITORY = Path(__file__).resolve().parents[2]
COLLECTOR = REPOSITORY / "scripts" / "collect-ifc-licenses.py"


class LicenseCollectionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.base = self.root / "private python"
        self.site = self.root / "site-packages"
        self.output = self.root / "collected licences"
        self.base.mkdir()
        self.site.mkdir()
        (self.base / "LICENSE.txt").write_bytes(b"Python fixture licence\r\n")
        (self.base / "python312.dll").write_bytes(b"private interpreter fixture")
        self.dist = self.site / "example-1.0.dist-info"
        self.dist.mkdir()
        self.metadata = self.dist / "METADATA"
        self.metadata.write_text(
            "Metadata-Version: 2.4\nName: example\nVersion: 1.0\n"
            "License-Expression: MIT\nLicense-File: LICENSE\n"
            "License-File: nested/AUTHORS.txt\n"
            "Project-URL: Source, https://example.org/source\n\nDescription\n",
            encoding="utf-8",
        )
        self.licences = self.dist / "licenses"
        (self.licences / "nested").mkdir(parents=True)
        (self.licences / "LICENSE").write_bytes(b"\xef\xbb\xbfFixture licence\r\n")
        (self.licences / "nested" / "AUTHORS.txt").write_bytes(b"Fixture authors\n")
        source = {
            "url": "https://example.org/source-1.0.tar.gz",
            "sha256": "a" * 64,
            "size": 123,
            "status": "identified",
        }
        self.catalogue = {
            "schemaVersion": 1,
            "components": [
                {
                    "name": "cpython",
                    "version": "3.12.13",
                    "licenseExpression": "Python-2.0",
                    "sources": [source.copy()],
                    "fingerprints": [
                        {
                            "path": "python312.dll",
                            "sha256": hashlib.sha256(
                                b"private interpreter fixture"
                            ).hexdigest(),
                        }
                    ],
                },
                {
                    "name": "example",
                    "version": "1.0",
                    "licenseExpression": "MIT",
                    "sources": [source.copy()],
                    "fingerprints": [],
                },
            ],
            "nativeComponents": [],
            "releaseBlockers": ["Fixture corresponding-source review is pending."],
        }
        self.catalogue_path = self.root / "provenance.json"

    def collect(self, output=None):
        self.catalogue_path.write_text(
            json.dumps(self.catalogue), encoding="utf-8"
        )
        return subprocess.run(
            [
                sys.executable,
                "-B",
                str(COLLECTOR),
                "--python-base",
                str(self.base),
                "--site-packages",
                str(self.site),
                "--output",
                str(output or self.output),
                "--provenance",
                str(self.catalogue_path),
                "--source-cache",
                str(self.root),
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
        )

    def test_collects_actual_bytes_and_portable_source_evidence(self):
        result = self.collect()
        self.assertEqual(result.returncode, 0, result.stderr)
        manifest_text = (self.output / "components.json").read_text(encoding="utf-8")
        manifest = json.loads(manifest_text)
        self.assertEqual(manifest["publicReleaseGate"]["status"], "blocked")
        component = next(
            item for item in manifest["components"] if item["name"] == "example"
        )
        self.assertEqual(component["version"], "1.0")
        self.assertEqual(component["licenseExpression"], "MIT")
        self.assertEqual(
            component["sources"][0]["url"],
            "https://example.org/source-1.0.tar.gz",
        )
        copied = [self.output / item["path"] for item in component["licenseFiles"]]
        self.assertIn(b"\xef\xbb\xbfFixture licence\r\n", [path.read_bytes() for path in copied])
        self.assertIn(b"Fixture authors\n", [path.read_bytes() for path in copied])
        for item in component["licenseFiles"]:
            self.assertEqual(
                hashlib.sha256((self.output / item["path"]).read_bytes()).hexdigest(),
                item["sha256"],
            )
        self.assertNotIn(str(self.root), manifest_text)
        self.assertTrue((self.output / "THIRD-PARTY-NOTICES.txt").is_file())
        self.assertTrue((self.output / "PUBLIC-RELEASE-GATE.md").is_file())

    def test_missing_licence_fails_before_writing_output(self):
        (self.licences / "LICENSE").unlink()
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("missing declared licence", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_missing_python_licence_fails(self):
        (self.base / "LICENSE.txt").unlink()
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("cpython licence", result.stderr.lower())

    def test_unknown_distribution_cannot_borrow_another_versions_provenance(self):
        self.metadata.write_text(
            self.metadata.read_text(encoding="utf-8").replace("Version: 1.0", "Version: 2.0"),
            encoding="utf-8",
        )
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("provenance", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_source_record_requires_digest_and_size(self):
        del self.catalogue["components"][1]["sources"][0]["sha256"]
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("source", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_interpreter_fingerprint_mismatch_fails(self):
        (self.base / "python312.dll").write_bytes(b"different interpreter")
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("fingerprint", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_declared_licence_path_cannot_escape_distribution(self):
        text = self.metadata.read_text(encoding="utf-8")
        self.metadata.write_text(
            text.replace("License-File: LICENSE", "License-File: ../../outside-LICENSE"),
            encoding="utf-8",
        )
        (self.root / "outside-LICENSE").write_text("unrelated file", encoding="utf-8")
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("unsafe", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_supplemental_upstream_licence_requires_matching_bytes(self):
        supplement = self.root / "upstream-LICENSE"
        supplement.write_bytes(b"different upstream text")
        self.catalogue["components"][1]["supplementalLicenses"] = [
            {
                "path": "upstream-LICENSE",
                "sha256": hashlib.sha256(b"authentic upstream text").hexdigest(),
                "url": "https://example.org/commit/LICENSE",
            }
        ]
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("licence digest", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_supplemental_texts_cover_wheel_with_no_packaged_licence(self):
        self.metadata.write_text(
            "Name: example\nVersion: 1.0\nLicense-Expression: MIT\n", encoding="utf-8"
        )
        (self.licences / "LICENSE").unlink()
        (self.licences / "nested" / "AUTHORS.txt").unlink()
        (self.root / "upstream-LICENSE").write_bytes(b"authentic upstream text")
        self.catalogue["components"][1]["supplementalLicenses"] = [
            {
                "path": "upstream-LICENSE",
                "sha256": hashlib.sha256(b"authentic upstream text").hexdigest(),
                "url": "https://example.org/commit/LICENSE",
            }
        ]
        result = self.collect()
        self.assertEqual(result.returncode, 0, result.stderr)
        manifest = json.loads((self.output / "components.json").read_text(encoding="utf-8"))
        component = next(item for item in manifest["components"] if item["name"] == "example")
        self.assertEqual(len(component["licenseFiles"]), 1)
        self.assertEqual(component["licenseFiles"][0]["origin"], "upstream")

    def test_nonempty_output_is_preserved(self):
        self.output.mkdir()
        (self.output / "keep.txt").write_bytes(b"keep this file")
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("nonempty", result.stderr.lower())
        self.assertEqual((self.output / "keep.txt").read_bytes(), b"keep this file")

    def archived_licence(self):
        archive_path = self.root / "source.tar.gz"
        content = b"actual source archive licence\r\n"
        with tarfile.open(archive_path, "w:gz") as archive:
            member = tarfile.TarInfo("source-1.0/COPYING")
            member.size = len(content)
            archive.addfile(member, io.BytesIO(content))
        self.catalogue["sourceArtifacts"] = {
            "source": {
                "cacheFile": "source.tar.gz",
                "url": "https://example.org/source-1.0.tar.gz",
                "sha256": hashlib.sha256(archive_path.read_bytes()).hexdigest(),
                "size": archive_path.stat().st_size,
                "status": "acquired",
            }
        }
        self.catalogue["components"][1]["archiveLicenses"] = [
            {
                "sourceId": "source",
                "member": "source-1.0/COPYING",
                "sha256": hashlib.sha256(content).hexdigest(),
            }
        ]
        return archive_path

    def test_copies_licence_from_digest_verified_source_archive(self):
        self.archived_licence()
        result = self.collect()
        self.assertEqual(result.returncode, 0, result.stderr)
        contents = [path.read_bytes() for path in self.output.rglob("COPYING")]
        self.assertIn(b"actual source archive licence\r\n", contents)

    def test_corrupt_source_archive_fails_before_writing_output(self):
        archive_path = self.archived_licence()
        archive_path.write_bytes(b"changed archive")
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("source archive", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_archive_licence_member_requires_matching_digest(self):
        self.archived_licence()
        self.catalogue["components"][1]["archiveLicenses"][0]["sha256"] = "b" * 64
        result = self.collect()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("licence digest", result.stderr.lower())
        self.assertFalse(self.output.exists())

    def test_output_cannot_be_inside_inputs(self):
        result = self.collect(self.site / "licences")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("overlap", result.stderr.lower())
        self.assertFalse((self.site / "licences").exists())

    def test_repeated_collection_has_identical_output(self):
        first = self.collect()
        second_output = self.root / "second output"
        second = self.collect(second_output)
        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(second.returncode, 0, second.stderr)
        first_files = {
            path.relative_to(self.output).as_posix(): path.read_bytes()
            for path in self.output.rglob("*") if path.is_file()
        }
        second_files = {
            path.relative_to(second_output).as_posix(): path.read_bytes()
            for path in second_output.rglob("*") if path.is_file()
        }
        self.assertEqual(first_files, second_files)

    def test_write_failure_never_publishes_incomplete_output(self):
        specification = importlib.util.spec_from_file_location("license_collector", COLLECTOR)
        collector = importlib.util.module_from_spec(specification)
        specification.loader.exec_module(collector)
        self.catalogue_path.write_text(json.dumps(self.catalogue), encoding="utf-8")
        original_open = Path.open
        written = 0

        def failing_open(path, mode="r", *arguments, **keywords):
            nonlocal written
            if mode == "xb":
                written += 1
                if written == 2:
                    raise OSError("simulated write failure")
            return original_open(path, mode, *arguments, **keywords)

        with patch.object(Path, "open", failing_open):
            with self.assertRaisesRegex(OSError, "simulated write failure"):
                collector.collect(
                    self.base, self.site, self.output, self.catalogue_path, self.root
                )
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
