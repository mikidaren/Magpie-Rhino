import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil


PYTHON_VERSION = "3.12.13"
IFC_VERSION = "0.9.0.post1"
PTH_CONTENT = ".\nDLLs\nLib\nLib/site-packages\n"
MSVC_WHEEL_PATH = "Lib/site-packages/shapely.libs/msvcp140-0fa7eb792d3fbcf2233e4ea47e9144b9.dll"
MSVC_WHEEL_SHA256 = "0fa7eb792d3fbcf2233e4ea47e9144b9b1309ba8ed5d4731a72fffa8f4f556d6"
EXCLUDED_PARTS = {"__pycache__", "test", "tests"}
EXCLUDED_STDLIB = {"site-packages", "ensurepip", "venv", "tkinter", "turtledemo", "idlelib"}


def _new_destination(source, target):
    source = Path(source).resolve()
    target = Path(target).resolve()
    if target == source or target.is_relative_to(source):
        raise ValueError("Runtime staging must be separate from its build interpreter")
    if target.exists() and any(target.iterdir()):
        raise ValueError("Runtime destination must be empty; existing files will not be overwritten")
    target.mkdir(parents=True, exist_ok=True)
    return source, target


def _copy_tree(source, target, excluded_top=()):
    source = Path(source)
    for entry in sorted(source.rglob("*")):
        relative = entry.relative_to(source)
        if entry.is_symlink() or entry.is_junction():
            raise ValueError("Runtime inputs must not contain filesystem links")
        if entry.is_dir():
            continue
        if any(part in EXCLUDED_PARTS for part in relative.parts) or relative.parts[0] in excluded_top:
            continue
        if entry.suffix in {".pyc", ".pyo"}:
            continue
        destination = target / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(entry, destination)


def copy_python_runtime(base, output):
    base = Path(base).resolve()
    required = [base / name for name in ("python.exe", "python312.dll", "LICENSE.txt", "Lib/encodings/__init__.py")]
    if any(not entry.is_file() for entry in required) or not (base / "DLLs").is_dir():
        raise ValueError("Build Python lacks the expected Windows CPython 3.12 runtime files")
    base, output = _new_destination(base, output)
    for entry in sorted(base.iterdir()):
        if entry.is_file() and (entry.name in {"python.exe", "python312.dll", "python3.dll", "LICENSE.txt", "BUILD"}
                                or entry.name.startswith("vcruntime") and entry.suffix == ".dll"):
            shutil.copy2(entry, output / entry.name)
    _copy_tree(base / "Lib", output / "Lib", EXCLUDED_STDLIB)
    for entry in sorted((base / "DLLs").iterdir()):
        if not entry.is_file() or entry.name.startswith(("_test", "_ctypes_test", "_tkinter", "tcl", "tk")):
            continue
        if entry.is_symlink():
            raise ValueError("Native runtime inputs must not be links")
        destination = output / "DLLs" / entry.name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(entry, destination)
    (output / "python312._pth").write_text(PTH_CONTENT, encoding="utf-8", newline="\n")


def copy_site_packages(source, output):
    source, output = _new_destination(source, output)
    forbidden = {"pip", "setuptools", "wheel", "easy_install.py", "bin", "Scripts"}
    _copy_tree(source, output, forbidden)


def _digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def stage_msvc_runtime(root):
    """Use the pinned wheel's unchanged signed binary as an app-local runtime."""
    root = Path(root).resolve()
    source = root / MSVC_WHEEL_PATH
    target = root / "msvcp140.dll"
    if (not source.is_file() or source.is_symlink() or source.is_junction()
            or _digest(source) != MSVC_WHEEL_SHA256):
        raise ValueError("The pinned Microsoft C++ runtime is missing or has changed")
    if target.exists():
        raise ValueError("The app-local Microsoft C++ runtime must not overwrite an existing file")
    shutil.copy2(source, target)


def create_manifest(root):
    root = Path(root).resolve()
    files = []
    for entry in sorted(root.rglob("*")):
        if entry.is_symlink() or entry.is_junction():
            raise ValueError("Runtime payload must not contain filesystem links")
        if "__pycache__" in entry.relative_to(root).parts or entry.suffix in {".pyc", ".pyo"}:
            raise ValueError("Runtime payload must not contain cached development bytecode; use -B")
        if not entry.is_file() or entry.name in {"manifest.json", "bundle-validation.json"}:
            continue
        files.append({"path": entry.relative_to(root).as_posix(), "size": entry.stat().st_size, "sha256": _digest(entry)})
    if not files:
        raise ValueError("Cannot create a manifest for an empty runtime")
    return {"schemaVersion": 1, "platform": "win-x64", "pythonVersion": PYTHON_VERSION,
            "ifcopenshellVersion": IFC_VERSION, "files": files}


def verify_manifest(root):
    root = Path(root).resolve()
    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))
    if (manifest.get("schemaVersion"), manifest.get("platform"), manifest.get("pythonVersion"),
        manifest.get("ifcopenshellVersion")) != (1, "win-x64", PYTHON_VERSION, IFC_VERSION):
        raise ValueError("Unsupported offline runtime manifest")
    seen = set()
    for entry in manifest.get("files", []):
        relative = entry["path"]
        path = PurePosixPath(relative)
        if "\\" in relative or path.is_absolute() or ":" in relative or ".." in path.parts or not path.parts:
            raise ValueError("Runtime manifest paths must stay inside the bundle")
        if relative.casefold() in seen:
            raise ValueError("Duplicate runtime manifest entry")
        seen.add(relative.casefold())
        target = root.joinpath(*path.parts)
        if not target.resolve().is_relative_to(root):
            raise ValueError("Runtime manifest escaped the bundle")
        for ancestor in (target, *target.parents):
            if ancestor == root.parent:
                break
            if ancestor.is_symlink() or ancestor.is_junction():
                raise ValueError("Runtime manifest contains filesystem links")
        if not target.is_file() or target.stat().st_size != entry["size"] or _digest(target) != entry["sha256"]:
            raise ValueError("Runtime file is missing or corrupted: " + relative)
    if not seen:
        raise ValueError("Runtime manifest has no payload")
    return manifest


def main():
    parser = argparse.ArgumentParser(description="Stage a relocatable private Windows IFC runtime")
    commands = parser.add_subparsers(dest="command", required=True)
    stage = commands.add_parser("stage")
    stage.add_argument("--python-base", required=True)
    stage.add_argument("--site-packages", required=True)
    stage.add_argument("--output", required=True)
    manifest_parser = commands.add_parser("manifest")
    manifest_parser.add_argument("--runtime", required=True)
    verify = commands.add_parser("verify")
    verify.add_argument("--runtime", required=True)
    arguments = parser.parse_args()
    if arguments.command == "stage":
        copy_python_runtime(arguments.python_base, arguments.output)
        copy_site_packages(arguments.site_packages, Path(arguments.output) / "Lib" / "site-packages")
        stage_msvc_runtime(arguments.output)
    elif arguments.command == "manifest":
        root = Path(arguments.runtime)
        (root / "manifest.json").write_text(json.dumps(create_manifest(root), indent=2) + "\n", encoding="utf-8")
    else:
        verify_manifest(arguments.runtime)
    print("Offline runtime " + arguments.command + " complete")


if __name__ == "__main__":
    main()
