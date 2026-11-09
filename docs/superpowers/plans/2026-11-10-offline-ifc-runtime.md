# Built-in Offline IFC Runtime Implementation Plan

> **For agentic workers:** Use test-driven implementation. The parent builds/packages the runtime and performs native/isolated verification. Bounded independent workers handle the portable resolver and component licence material.

**Goal:** Ship a Windows x64 plugin that performs verified IFC exchange without external Python or first-use network installation.

**Architecture:** Bundle a relocatable private CPython and pinned IfcOpenShell dependencies. Resolve only the package-local runtime and execute the existing worker in an isolated subprocess. Preserve the current geometric kernel and gateway behavior.

**Tech Stack:** .NET 8, CPython 3.12.13, IfcOpenShell 0.9.0.post1, PowerShell and existing Rhino/Yak packaging.

**Spec:** `docs/superpowers/specs/2026-11-10-offline-ifc-runtime-design.md`.

## Global constraints

- Windows x64 complete offline package, version 0.4.1; preserve existing GUIDs and settings.
- No global interpreter installation, PATH change, user-config deletion or runtime download.
- Worker process stays separate from Rhino and receives no ambient provider credentials.
- Protect all open user models; use owned hidden QA instances.
- Licences/source provenance accompany distributed dependencies; no invented source offer or public release.
- Do not commit/push the existing main working tree or modify the published warehouse kernel.

## Review focus

- Missing/corrupt runtime cannot silently use a system or developer Python.
- Relative-path manifest entries cannot escape through traversal or reparse points.
- Runtime package contains no venv anchors, pip or injected Python search paths.
- Offline export has the exact same semantic/geometric validation and no-overwrite guarantees.
- Dependency notices and source provenance are explicit; isolated-machine evidence is not overstated.

## Tasks

- [x] Test and implement fixed bundle resolution and isolated subprocess construction.
- [x] Test and implement runtime staging, pinned dependencies and manifest creation.
- [x] Collect available component licences and source provenance without fabricated legal assurances; public redistribution remains blocked pending review.
- [x] Integrate the resolver, bundle-aware packaging and CI; remove manual setup from the shipped flow.
- [x] Run isolated offline export/readback from unpacked package bytes and exercise the C# launcher.
- [x] Install 0.4.1 after user permission; verify startup and native menu selection in an owned Rhino process and exercise the panel's export action with a supplied test path. The interactive save dialog was not exercised.
- [x] Complete local package verification and record checksums; public release remains blocked and native Rhino UI validation remains pending.

## Ledger

- 2026-11-10: user approved the previously proposed complete offline bundle. Existing 0.4.0 changes remain uncommitted; keep them intact and layer this focused packaging milestone on top.
- Ruling: use private relocatable CPython plus external wheel files rather than embedding Python into Rhino or making an opaque one-file frozen worker. This preserves dependency isolation and easier component replacement/licence inspection.
- Windows Sandbox is not available at the inspected system path. An isolated subprocess test can prove dependency origins, but cannot be reported as a clean Windows VM run.
- Review found that the previous candidate silently relied on System32 msvcp140.dll. The current builder copies the unchanged, signed, hash-pinned Shapely wheel copy into the private application directory. Unit checks reject a missing/corrupt DLL; isolated baseline, shared-window and upper-storey tests require and observe its app-local loading. A Windows system msvcp_win.dll remains a platform dependency.
- 2026-11-10: 35 packaging checks and 268 agent checks passed. Ten Windows symbolic-link cases are skipped by local privileges; a real directory-junction rejection was verified separately. IFC worker tests recorded 43 passes.
- 2026-11-10: the user authorized installation. Yak reports 0.4.1; installed .rhp and assemblies match the built hashes. An owned hidden Rhino process opened a copy of the two-storey example, loaded 0.4.1 at startup, displayed Magpie in the native agent menu without issuing the Magpie command, and exported IFC via the hosted panel with a supplied test path. The validation report passed with zero failed checks. The native file-selection dialog was not exercised; the QA process was closed.
