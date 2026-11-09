import importlib.util
from pathlib import Path
import subprocess
import struct
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "ifc_runtime_probe.py"


class RuntimeProbeTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(SCRIPT.is_file(), "The isolated offline runtime probe is not implemented")
        spec = importlib.util.spec_from_file_location("ifc_runtime_probe", SCRIPT)
        self.probe = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.probe)

    def test_a_development_interpreter_is_not_accepted_as_a_private_runtime(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(RuntimeError):
                self.probe.verify_interpreter(Path(directory))

    def test_local_hostname_query_is_not_treated_as_network_access(self):
        self.assertFalse(self.probe.network_event_forbidden("socket.gethostname"))
        for event in ("socket.__new__", "socket.connect", "socket.bind", "socket.getaddrinfo",
                      "socket.gethostbyname", "socket.gethostbyaddr", "urllib.Request"):
            self.assertTrue(self.probe.network_event_forbidden(event), event)

    def test_native_import_table_identifies_required_libraries(self):
        payload = bytearray(0x500)
        payload[:2] = b"MZ"
        struct.pack_into("<I", payload, 0x3C, 0x80)
        payload[0x80:0x84] = b"PE\0\0"
        struct.pack_into("<H", payload, 0x86, 1)
        struct.pack_into("<H", payload, 0x94, 0xF0)
        struct.pack_into("<H", payload, 0x98, 0x20B)
        struct.pack_into("<I", payload, 0x98 + 108, 16)
        struct.pack_into("<II", payload, 0x98 + 112 + 8, 0x1000, 40)
        struct.pack_into("<IIII", payload, 0x188 + 8, 0x200, 0x1000, 0x200, 0x200)
        struct.pack_into("<I", payload, 0x200 + 12, 0x1080)
        payload[0x280:0x28E] = b"python312.dll\0"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "sample.pyd"
            path.write_bytes(payload)
            self.assertEqual(self.probe.pe_imports(path), {"python312.dll"})

    def test_invalid_native_binary_is_not_accepted_as_dependency_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "broken.dll"
            path.write_bytes(b"not a PE file")
            with self.assertRaises(ValueError):
                self.probe.pe_imports(path)

    def test_required_msvc_must_be_bundled_and_actually_loaded_from_bundle(self):
        self.assertTrue(callable(getattr(self.probe, "require_private_msvc", None)))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runtime = root / "private runtime"
            runtime.mkdir()
            host = root / "windows" / "msvcp140.dll"
            host.parent.mkdir()
            host.write_bytes(b"system runtime")
            with self.assertRaisesRegex(RuntimeError, "msvcp140"):
                self.probe.require_private_msvc(runtime, {"msvcp140.dll"}, [host])
            bundled = runtime / "msvcp140.dll"
            bundled.write_bytes(b"bundled runtime")
            with self.assertRaisesRegex(RuntimeError, "msvcp140"):
                self.probe.require_private_msvc(runtime, {"msvcp140.dll"}, [host])
            self.probe.require_private_msvc(runtime, {"msvcp140.dll"}, [bundled])

    def test_subprocess_negative_probe_fails_before_claiming_export_success(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run([sys.executable, "-I", "-S", "-B", str(SCRIPT), "--runtime", directory,
                                     "--worker", str(Path(directory) / "missing.py"),
                                     "--project", str(Path(directory) / "missing.json"),
                                     "--output", str(Path(directory) / "never.ifc"),
                                     "--report", str(Path(directory) / "result.json")], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse((Path(directory) / "never.ifc").exists())
            self.assertNotIn('"verified": true', result.stdout)


if __name__ == "__main__":
    unittest.main()
