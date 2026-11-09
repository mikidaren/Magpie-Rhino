import argparse
import contextlib
import ctypes
import importlib
import importlib.metadata
import importlib.util
import io
import json
import mmap
import os
from pathlib import Path
import runpy
import struct
import sys


EXPECTED_PACKAGES = {
    "ifcopenshell": ("ifcopenshell", "0.9.0.post1"), "isodate": ("isodate", "0.7.2"),
    "lark": ("lark", "1.3.1"), "numpy": ("numpy", "2.5.4"),
    "pyparsing": ("pyparsing", "3.3.3"), "python-dateutil": ("dateutil", "4.0.0"),
    "shapely": ("shapely", "2.2.0"), "typing_extensions": ("typing_extensions", "4.16.0"),
    "tzdata": ("tzdata", "2026.6"),
}


def network_event_forbidden(event):
    return event == "urllib.Request" or event.startswith("socket.") and event != "socket.gethostname"


def verify_interpreter(runtime):
    runtime = Path(runtime).resolve()
    if Path(sys.prefix).resolve() != runtime or Path(sys.base_prefix).resolve() != runtime:
        raise RuntimeError("The executing interpreter is not the private bundle")
    if Path(sys.executable).resolve() != runtime / "python.exe":
        raise RuntimeError("A system or development interpreter was selected")
    if sys.version_info[:3] != (3, 12, 13) or not sys.flags.isolated or not sys.flags.no_site or not sys.dont_write_bytecode:
        raise RuntimeError("The private interpreter is not the fixed isolated runtime")
    if importlib.util.find_spec("pip") is not None:
        raise RuntimeError("The shipped runtime must not contain pip")
    paths = []
    for entry in sys.path:
        resolved = Path(entry).resolve()
        if not resolved.is_relative_to(runtime):
            raise RuntimeError("Python imports can escape the bundle")
        paths.append(resolved.relative_to(runtime).as_posix())
    return {"pythonVersion": "3.12.13", "privatePrefix": True, "isolated": True,
            "siteDisabled": True, "bytecodeDisabled": True, "pipAbsent": True, "searchPaths": paths}


def pe_imports(path):
    with Path(path).open("rb") as stream:
        if stream.seek(0, 2) < 64:
            raise ValueError("Native binary has no valid PE header")
        with mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
            try:
                if data[:2] != b"MZ":
                    raise ValueError("Native binary is not a PE file")
                header = struct.unpack_from("<I", data, 0x3C)[0]
                if data[header:header + 4] != b"PE\0\0":
                    raise ValueError("Native binary has an invalid PE signature")
                sections_count = struct.unpack_from("<H", data, header + 6)[0]
                optional_size = struct.unpack_from("<H", data, header + 20)[0]
                optional = header + 24
                magic = struct.unpack_from("<H", data, optional)[0]
                if magic not in (0x10B, 0x20B):
                    raise ValueError("Unsupported native PE format")
                directories = optional + (112 if magic == 0x20B else 96)
                directory_count = struct.unpack_from("<I", data, directories - 4)[0]
                image_base = struct.unpack_from("<Q" if magic == 0x20B else "<I", data,
                                                optional + (24 if magic == 0x20B else 28))[0]
                sections = []
                for index in range(sections_count):
                    section = optional + optional_size + index * 40
                    virtual_size, address, raw_size, offset = struct.unpack_from("<IIII", data, section + 8)
                    sections.append((address, max(virtual_size, raw_size), offset, raw_size))

                def file_offset(address):
                    for start, length, offset, raw_size in sections:
                        if start <= address < start + length and address - start < raw_size:
                            return offset + address - start
                    raise ValueError("Native import address is outside file sections")

                def library_name(address):
                    offset = file_offset(address)
                    end = data.find(b"\0", offset, offset + 512)
                    if end < offset:
                        raise ValueError("Native import name is unterminated")
                    return data[offset:end].decode("ascii").lower()

                libraries = set()
                for directory_index, descriptor_size, name_offset in ((1, 20, 12), (13, 32, 4)):
                    if directory_index >= directory_count:
                        continue
                    address, length = struct.unpack_from("<II", data, directories + directory_index * 8)
                    if not address or not length:
                        continue
                    offset = file_offset(address)
                    for descriptor in range(offset, offset + length, descriptor_size):
                        if not any(data[descriptor:descriptor + descriptor_size]):
                            break
                        name_address = struct.unpack_from("<I", data, descriptor + name_offset)[0]
                        if directory_index == 13 and struct.unpack_from("<I", data, descriptor)[0] & 1 == 0:
                            name_address -= image_base
                        libraries.add(library_name(name_address))
                return libraries
            except (struct.error, UnicodeDecodeError, IndexError) as error:
                raise ValueError("Native import table is malformed") from error


def require_private_msvc(runtime, required_names, loaded_paths):
    if "msvcp140.dll" not in {name.lower() for name in required_names}:
        return
    expected = Path(runtime).resolve() / "msvcp140.dll"
    if not expected.is_file():
        raise RuntimeError("The private bundle is missing msvcp140.dll")
    loaded = [Path(path).resolve() for path in loaded_paths if Path(path).name.lower() == "msvcp140.dll"]
    if len(loaded) != 1 or loaded[0] != expected:
        raise RuntimeError("The worker did not load msvcp140.dll from the private bundle")


def native_modules(runtime):
    if os.name != "nt":
        return {"checked": False, "reason": "Windows module enumeration is unavailable"}
    from ctypes import wintypes
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    process_api = ctypes.WinDLL("psapi", use_last_error=True)
    kernel.GetCurrentProcess.restype = wintypes.HANDLE
    process_api.EnumProcessModules.argtypes = (wintypes.HANDLE, ctypes.POINTER(wintypes.HMODULE),
                                              wintypes.DWORD, ctypes.POINTER(wintypes.DWORD))
    process_api.EnumProcessModules.restype = wintypes.BOOL
    kernel.GetModuleFileNameW.argtypes = (wintypes.HMODULE, wintypes.LPWSTR, wintypes.DWORD)
    kernel.GetModuleFileNameW.restype = wintypes.DWORD
    handles = (wintypes.HMODULE * 1024)()
    needed = wintypes.DWORD()
    if not process_api.EnumProcessModules(kernel.GetCurrentProcess(), handles, ctypes.sizeof(handles), ctypes.byref(needed)):
        raise RuntimeError("Cannot enumerate native runtime dependencies")
    windows = Path(os.environ.get("SystemRoot", "C:/Windows")).resolve()
    native_files = [entry for entry in runtime.rglob("*") if entry.suffix.lower() in {".exe", ".dll", ".pyd"}]
    bundled_names = {entry.name.lower() for entry in native_files}
    required_names = set()
    for entry in native_files:
        required_names.update(pe_imports(entry))
    unresolved = sorted(name for name in required_names if name not in bundled_names
                        and not name.startswith(("api-ms-", "ext-ms-"))
                        and not (windows / "System32" / name).is_file())
    if unresolved:
        raise RuntimeError("Native imports are absent from the bundle and Windows: " + ", ".join(unresolved))
    results = []
    prerequisites = []
    ambient = []
    loaded_paths = []
    for index in range(needed.value // ctypes.sizeof(wintypes.HMODULE)):
        buffer = ctypes.create_unicode_buffer(32768)
        if not kernel.GetModuleFileNameW(handles[index], buffer, len(buffer)):
            raise RuntimeError("Cannot identify a loaded native dependency")
        path = Path(buffer.value).resolve()
        loaded_paths.append(path)
        if path.is_relative_to(runtime):
            results.append("bundle:" + path.relative_to(runtime).as_posix())
        elif path.is_relative_to(windows):
            results.append("windows:" + path.relative_to(windows).as_posix())
            if path.name.lower().startswith(("msvcp", "vcruntime")):
                prerequisites.append(path.name)
        else:
            if path.name.lower() in required_names:
                raise RuntimeError("A required native library came from outside the bundle or Windows: " + path.name)
            ambient.append(path.name)
    require_private_msvc(runtime, required_names, loaded_paths)
    return {"checked": True, "libraries": sorted(results), "windowsRuntimePrerequisites": sorted(prerequisites),
            "staticImportsResolved": True, "ambientNativeModules": sorted(ambient),
            "hostIsCleanVm": False}


def main():
    parser = argparse.ArgumentParser(description="Prove IFC export with a private isolated runtime")
    for name in ("runtime", "worker", "project", "output", "report"):
        parser.add_argument("--" + name, required=True)
    options = parser.parse_args()
    report_path = Path(options.report)
    if report_path.exists():
        raise RuntimeError("The probe will not overwrite an existing report")
    runtime = Path(options.runtime).resolve()
    report = {"schemaVersion": 1, "verified": False, "freshWindowsVm": False,
              "interpreter": verify_interpreter(runtime)}
    network_events = []

    def deny_network(event, arguments):
        if network_event_forbidden(event):
            network_events.append(event)
            raise RuntimeError("Network access is forbidden in the offline runtime probe")

    sys.addaudithook(deny_network)
    origins = {}
    for name in ("json", "hashlib", "pathlib", "ctypes", "sysconfig"):
        module = importlib.import_module(name)
        module_path = Path(module.__file__).resolve()
        if not module_path.is_relative_to(runtime):
            raise RuntimeError("Standard library escaped the private runtime")
        origins[name] = module_path.relative_to(runtime).as_posix()
    versions = {}
    for distribution, (module_name, expected) in EXPECTED_PACKAGES.items():
        module = importlib.import_module(module_name)
        module_path = Path(module.__file__).resolve()
        if not module_path.is_relative_to(runtime):
            raise RuntimeError("A third-party package was imported from outside the bundle")
        version = importlib.metadata.version(distribution)
        if version != expected:
            raise RuntimeError("A bundled dependency has an unexpected version: " + distribution)
        versions[distribution] = version
        origins[module_name] = module_path.relative_to(runtime).as_posix()
    worker = Path(options.worker).resolve()
    if worker != runtime.parent / "miki_ifc.py":
        raise RuntimeError("The probe worker must be the shipped package sibling")
    validation_path = Path(str(options.output) + ".validation.json")
    original_arguments = sys.argv
    sys.argv = [str(worker), "export", "--project", options.project, "--output", options.output,
                "--report", str(validation_path)]
    output = io.StringIO()
    try:
        with contextlib.redirect_stdout(output):
            try:
                runpy.run_path(str(worker), run_name="__main__")
            except SystemExit as result:
                if result.code not in (None, 0):
                    raise RuntimeError("Bundled worker failed: " + output.getvalue()) from result
    finally:
        sys.argv = original_arguments
    validation = json.loads(validation_path.read_text(encoding="utf-8"))
    if validation.get("verified") is not True:
        raise RuntimeError("The exported IFC did not pass actual geometry readback")
    report.update({"verified": True, "moduleOrigins": origins, "versions": versions,
                   "networkGuard": "Python audit hooks reject socket/network operations; local gethostname is allowed",
                   "networkEvents": network_events, "nativeDependencies": native_modules(runtime),
                   "ifc": {name: validation[name] for name in ("verified", "revision", "ifcSchema", "counts", "failedChecks")}})
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"verified": True, "isolated": True, "pipAbsent": True,
                      "ifcCounts": validation["counts"], "freshWindowsVm": False}))


if __name__ == "__main__":
    main()
