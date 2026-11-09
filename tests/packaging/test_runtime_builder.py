import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "ifc_runtime_builder.py"


class RuntimeBuilderTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(SCRIPT.is_file(), "The relocatable IFC runtime builder is not implemented")
        spec = importlib.util.spec_from_file_location("ifc_runtime_builder", SCRIPT)
        self.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.builder)
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.base = self.root / "build python"
        for relative in ("python.exe", "python312.dll", "python3.dll", "vcruntime140.dll", "LICENSE.txt",
                         "DLLs/_ctypes.pyd", "DLLs/_tkinter.pyd", "DLLs/_testcapi.pyd", "DLLs/tcl86t.dll",
                         "Lib/encodings/__init__.py", "Lib/json/__init__.py", "Lib/ctypes/__init__.py",
                         "Lib/test/test_old.py", "Lib/tkinter/__init__.py", "Lib/ensurepip/pip.whl",
                         "Lib/site-packages/pip/__init__.py", "Lib/json/__pycache__/cached.pyc"):
            target = self.base / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"test payload")
        self.output = self.root / "private runtime"

    def test_private_copy_has_only_relative_import_roots_without_site(self):
        self.builder.copy_python_runtime(self.base, self.output)
        self.assertEqual((self.output / "python312._pth").read_text().splitlines(),
                         [".", "DLLs", "Lib", "Lib/site-packages"])
        self.assertTrue((self.output / "Lib/encodings/__init__.py").is_file())
        self.assertTrue((self.output / "DLLs/_ctypes.pyd").is_file())

    def test_development_venv_pip_tk_tests_and_bytecode_are_not_copied(self):
        self.builder.copy_python_runtime(self.base, self.output)
        for relative in ("Lib/site-packages/pip", "Lib/ensurepip", "Lib/test", "Lib/tkinter",
                         "DLLs/_tkinter.pyd", "DLLs/tcl86t.dll", "DLLs/_testcapi.pyd", "Lib/json/__pycache__"):
            self.assertFalse((self.output / relative).exists(), relative)
        self.assertFalse((self.output / "pyvenv.cfg").exists())

    def test_existing_payload_is_not_overwritten(self):
        self.output.mkdir()
        (self.output / "keep.txt").write_text("keep")
        with self.assertRaises(ValueError):
            self.builder.copy_python_runtime(self.base, self.output)
        self.assertEqual((self.output / "keep.txt").read_text(), "keep")

    def test_copy_inside_build_interpreter_is_refused(self):
        with self.assertRaises(ValueError):
            self.builder.copy_python_runtime(self.base, self.base / "bad-output")

    def test_package_copy_keeps_licences_but_not_test_or_bytecode_payloads(self):
        source = self.root / "wheel installation"
        for relative in ("ifcopenshell/__init__.py", "ifcopenshell/_kernel.pyd",
                         "ifcopenshell/tests/test_case.py", "ifcopenshell/__pycache__/cached.pyc",
                         "ifcopenshell-0.9.0.post1.dist-info/licenses/COPYING"):
            target = source / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"wheel")
        self.builder.copy_site_packages(source, self.output)
        self.assertTrue((self.output / "ifcopenshell/_kernel.pyd").is_file())
        self.assertTrue((self.output / "ifcopenshell-0.9.0.post1.dist-info/licenses/COPYING").is_file())
        self.assertFalse((self.output / "ifcopenshell/tests").exists())
        self.assertFalse((self.output / "ifcopenshell/__pycache__").exists())

    def test_stages_pinned_msvc_from_bundled_wheel_without_changing_bytes(self):
        self.assertTrue(callable(getattr(self.builder, "stage_msvc_runtime", None)))
        source = self.output / "Lib/site-packages/shapely.libs/msvcp140-test.dll"
        source.parent.mkdir(parents=True)
        source.write_bytes(b"signed runtime payload")
        self.builder.MSVC_WHEEL_PATH = "Lib/site-packages/shapely.libs/msvcp140-test.dll"
        self.builder.MSVC_WHEEL_SHA256 = hashlib.sha256(source.read_bytes()).hexdigest()
        self.builder.stage_msvc_runtime(self.output)
        self.assertEqual((self.output / "msvcp140.dll").read_bytes(), source.read_bytes())

    def test_staging_msvc_rejects_missing_or_changed_binary(self):
        self.assertTrue(callable(getattr(self.builder, "stage_msvc_runtime", None)))
        self.output.mkdir()
        self.builder.MSVC_WHEEL_PATH = "Lib/site-packages/shapely.libs/msvcp140-test.dll"
        self.builder.MSVC_WHEEL_SHA256 = hashlib.sha256(b"expected").hexdigest()
        with self.assertRaises(ValueError):
            self.builder.stage_msvc_runtime(self.output)
        source = self.output / self.builder.MSVC_WHEEL_PATH
        source.parent.mkdir(parents=True)
        source.write_bytes(b"changed")
        with self.assertRaises(ValueError):
            self.builder.stage_msvc_runtime(self.output)
        self.assertFalse((self.output / "msvcp140.dll").exists())

    def test_manifest_hashes_detect_corrupted_payload(self):
        self.builder.copy_python_runtime(self.base, self.output)
        manifest = self.builder.create_manifest(self.output)
        self.assertEqual(manifest["pythonVersion"], "3.12.13")
        self.assertEqual(manifest["ifcopenshellVersion"], "0.9.0.post1")
        self.assertTrue(all("\\" not in entry["path"] for entry in manifest["files"]))
        (self.output / "manifest.json").write_text(json.dumps(manifest))
        self.builder.verify_manifest(self.output)
        (self.output / "python312.dll").write_bytes(b"changed")
        with self.assertRaises(ValueError):
            self.builder.verify_manifest(self.output)

    def test_manifest_cannot_point_outside_the_runtime(self):
        self.builder.copy_python_runtime(self.base, self.output)
        manifest = self.builder.create_manifest(self.output)
        manifest["files"][0]["path"] = "../outside.dll"
        (self.output / "manifest.json").write_text(json.dumps(manifest))
        with self.assertRaises(ValueError):
            self.builder.verify_manifest(self.output)

    def test_development_bytecode_is_never_accepted_in_a_release_manifest(self):
        self.builder.copy_python_runtime(self.base, self.output)
        cached = self.output / "Lib" / "json" / "__pycache__" / "development.pyc"
        cached.parent.mkdir(parents=True)
        cached.write_bytes(b"compiled development path")
        with self.assertRaises(ValueError):
            self.builder.create_manifest(self.output)


if __name__ == "__main__":
    unittest.main()
