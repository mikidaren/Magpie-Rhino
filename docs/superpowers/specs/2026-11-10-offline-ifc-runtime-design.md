# Built-in offline IFC runtime

Approved: the user accepted a complete Windows x64 offline runtime and instructed continuation on 2026-11-10. Implement without another approval round.

## Goal

Install the Rhino plugin once and export/verify IFC without system Python, pip, development-directory paths or a first-use download. Keep exact Rhino/NURBS-compatible modeling and the current independent IFC worker process. Do not embed a Python interpreter into Rhino's process.

## Layout and contract

The package contains `openbim/miki_ifc.py`, its pinned source snapshot, and `openbim/runtime/`:

- Private relocatable CPython 3.12.13 Windows x64, `python.exe`, `python312.dll`, its standard library and native DLLs.
- An isolated `python312._pth` with only `.`, `DLLs`, `Lib` and `Lib/site-packages`; no `import site` or external paths.
- Exact pinned wheels: IfcOpenShell 0.9.0.post1 and the currently validated nine-package dependency set.
- `manifest.json`: `{schemaVersion:1,platform:"win-x64",pythonVersion:"3.12.13",ifcopenshellVersion:"0.9.0.post1",files:[{path,size,sha256}]}`. Paths are relative forward-slash paths inside this runtime. The manifest itself is excluded from its file list.
- Component licences, source provenance and corresponding-source acquisition material. These must not invent a legal source offer on the owner's behalf. Public binary release remains separate from local testing and requires the licence review gate.

Use the relocatable base interpreter, not a copied virtual environment. Do not ship `pyvenv.cfg`, pip, test suites, Tk, cached bytecode or development paths. Keep the dependencies as replaceable external files, not an opaque frozen archive. The generated manifest detects corruption; it is not a signature or a DRM mechanism. Document how a compatible modified LGPL component can be repackaged with a new manifest.

## Resolver

Portable agent API: `IfcRuntimeLocation` record with `RootDirectory`, `PythonPath`, `WorkerPath`, `PythonVersion`, `IfcVersion`; static `IfcRuntimeBundle.Resolve(string pluginDirectory)` validates the fixed bundle location and returns it. `IfcRuntimeBundle.IsPresent(string pluginDirectory)` supplies a nonthrowing catalogue availability check. `IfcRuntimeBundle.CreateStartInfo(IfcRuntimeLocation, IEnumerable<string> arguments)` starts `python.exe -I -S WORKER ...` with `UseShellExecute=false`, no window and redirected output. It uses a minimal environment rather than inheriting provider credentials or Python search paths.

Do not read or silently fall back to `openbim-runtime.json`, a system interpreter or a development venv. A missing/corrupt bundle produces actionable reinstall guidance, not pip instructions or a download. Preserve the existing export approval, no-overwrite, timeout, verified readback, revision and error-reporting contracts.

## Build and packaging

`scripts/build-ifc-runtime.ps1` accepts an explicit build Python and uv executable, creates a private runtime under `dist/ifc-runtime/win-x64`, installs pinned wheels there at build time, gathers licences and produces the manifest. Never recursively delete a computed path without validating its final absolute location within the named staging directory. Prefer refusing a nonempty staging directory or using a new stage.

`scripts/build-panel.ps1` requires a complete verified runtime and packages it under `openbim/runtime`. The release ZIP does not include the manual setup script. Existing old preference files stay untouched but are not used by this runtime. CI must build and smoke-test the runtime before publishing an artifact.

The product version is 0.4.1. Keep the plugin GUID, Yak identity, Magpie gateway contract and native AI entry unchanged. No provider, branding, donation or architecture-kernel changes in this milestone.

## Verification

- Resolver tests first: fixed path selection, corrupted/missing files, path traversal, duplicates, unsupported platform/version, isolated launch and no ambient credential inheritance.
- Run the bundled interpreter from a fresh directory/profile with a deliberately unusable PATH, bogus Python environment and no pip; assert `sys.prefix`, stdlib and third-party module origins stay inside the bundle.
- Use only bundle contents to export and reopen the generic two-storey model offline. Retest updated shared windows and second-storey height with the same existing source JSON and IFC checks.
- Unpack the actual Yak/ZIP into a path containing spaces and verify the packaged runtime again. Remove or invalidate the developer runtime configuration in an owned test profile, not by deleting the user's preference file.
- Verify native Rhino's export service chooses the packaged runtime and succeeds. Use an owned hidden Rhino instance, never an open user design.
- Run existing tests, inspect archive entries and compare installed/packaged hashes.
- A PATH-isolated test is not a fresh Windows VM. Check Windows Sandbox availability and report this limitation if unavailable; never claim a test that did not happen.

## Delivery

Produce a complete local Windows x64 installer ZIP/Yak, a runtime smoke-test report, licences/source provenance and SHA256 checksums. Do not push the main product, create a public release or update marketplaces without a separate instruction.
