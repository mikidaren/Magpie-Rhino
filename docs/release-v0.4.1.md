# Local-only 0.4.1: built-in offline IFC

This candidate bundles a private relocatable Windows x64 CPython 3.12.13 and nine version/hash-pinned wheels. No system Python, pip, developer venv or first-use package download is part of the user workflow. Rhino's exact geometry and the 0.4.0 architectural kernel are unchanged.

The worker remains a separate process. Its fixed local resolver validates the runtime manifest and pinned worker source; subprocess arguments are shell-free and use `-I -S -B` with a cleared environment. A damaged bundle does not fall back to external configuration or a system interpreter.

The panel adds a deterministic IFC export action. File selection and verified export do not need a model or gateway. Existing approvals, no-overwrite policy, document checks, timeout and geometric readback remain in the shared tool service. Cloud AI itself is not made offline by this change.

Validation removes PATH, supplies a fresh profile and poisons external Python search settings. Python audit hooks reject network operations while permitting local hostname lookup required by platform inspection. Imported stdlib and package paths stay under the private runtime; Windows PE import dependencies are checked. The generic two-storey baseline, shared-window change and upper-storey height change export and reopen successfully. This is not a claim that a fresh Windows virtual machine was tested; Windows Sandbox is unavailable on the inspected host. An unrelated host audio utility injects a DLL and is recorded separately from runtime import dependencies.

The required `msvcp140.dll` is staged app-locally from the pinned Shapely wheel without changing its signed bytes. The isolation probe requires that exact app-local path to be loaded; the smoke-test host also uses its Windows `msvcp_win.dll`. Microsoft redistribution terms remain part of the unresolved public-release review.

Third-party notices and source provenance accompany the bundle. The audit found native CGAL and other unresolved redistribution/source-correspondence issues. The public binary release gate remains blocked, without inventing a source-offer contract for the owner. Local-only packaging must be selected explicitly; CI does not upload public binaries or publish a Release until the review is approved.

The public-distribution evidence, source acquisitions, corrected RocksDB/Zstd inventory and controlled OCC-only alternative are documented in [the 0.4.1 licensing review](licensing/2026-11-10-ifc-public-distribution-review.md). Its completion criteria apply to the exact final binaries, paired corresponding-source assets and refreshed notices before public readiness can be approved.

No main repository push, marketplace change, global interpreter installation or user-setting migration is part of this milestone.
