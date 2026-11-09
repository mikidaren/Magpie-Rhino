# Lightweight OpenBIM Implementation Plan

> **For agentic workers:** Use test-driven implementation and bounded independent workers for the warehouse domain and IFC worker; the parent owns the native adapter and end-to-end proof. Steps use checkbox syntax for tracking.

**Goal:** Prove and integrate a minimal two-storey architectural model with exact Rhino geometry, shared Blocks, scoped parametric updates and verified IFC exchange.

**Architecture:** A portable .NET project engine in the reusable warehouse supplies one JSON contract. A RhinoCommon adapter creates/updates actual native objects. An optional pinned IfcOpenShell worker exports/reopens IFC. The current panel dispatches compact local tools without sending reusable source code to the LLM.

**Tech Stack:** .NET 8, RhinoCommon 9, Python 3.12, IfcOpenShell, existing HTML/Eto panel.

**Spec:** `docs/superpowers/specs/2026-11-09-lightweight-openbim-design.md`.

## Global Constraints

- Preserve NURBS-compatible curves, native Breps, exact document-unit conversion and existing Magpie installation identities.
- Input contract is version 1, metres; no construction-detail/material specifications are required.
- Never modify the user's active unsaved document; native tests use an owned process.
- No source-code payloads in model history, no automatic runtime installation/download, no unrequested export overwrite.
- Preserve scale approvals, persistent YOLO choice, native undo and explicit failure resolution.
- Keep user observation data and machine-specific runtime configuration out of public source.
- No main-repository commit, branch, release or marketplace change in this milestone.

## Review Focus

- Invalid/overlapping openings and missing references must fail before document changes.
- Unit conversion, manually edited objects and metadata/geometry divergence must fail safely.
- Shared-type edits must update every matching instance without unrelated geometry churn.
- IFC geometry must contain real openings and mapped shared types after reopening.
- Missing runtime, declined export, cancellation and local-tool errors must not bypass approvals or clear unrelated failures.

## Task 1: Portable project engine

- [x] Write failing domain tests for demo, minimal parameters, invalid topology, patch merge and affected-ID scope.
- [x] Implement `BuildingProject` records and `ProjectEngine` under `Miki-GrasshopperWarehouse/src/Miki.OpenBim`.
- [x] Run the complete domain test executable and generate the generic demo JSON.

## Task 2: IFC exchange worker

- [x] Write failing Python tests for two storeys, wall voids/fillings, shared types, identity and actual geometry readback.
- [x] Implement `python/miki_ifc.py` against the same contract and pin dependencies.
- [x] Run export/readback tests, including invalid input and modified type/height.

## Task 3: Native exact geometry

- [x] Write a failing native validation script for an empty owned document.
- [x] Implement `Miki.OpenBim.Rhino` adapter, shared definitions, source metadata and scoped apply/inspect.
- [x] Run before/after sample and shared-Block/height updates; save `.3dm` artifacts.
- [x] Verify actual bounds, volume, solid validity, identity, units and unchanged objects.

## Task 4: Local-tool integration

- [x] Add failing agent tests for optional local dispatch, approval, basis gating, readback and failures.
- [x] Add `ILocalModelingTools` and session dispatch without changing native MCP behavior.
- [x] Supply the document-scoped service from the panel and run one native hosted request.
- [x] Keep IFC worker configuration separate from Magpie-managed keys.

## Task 5: Package and evidence

- [x] Run existing core/agent/workflow/browser tests and new warehouse tests.
- [x] Reopen saved Rhino and IFC files and write a compact validation report with real viewport captures.
- [x] Package the verified warehouse/runtime source and local plugin candidate.
- [x] Review changes; create the requested reusable GitHub repository only with generic validated source/demo.

## Execution ledger

- 2026-11-09: user explicitly approved implementation. Proceed without another planning approval round.
- Ruling: implement only simple architectural semantics and the two-storey proof; branding/provider/donation redesign is deferred to avoid scope creep.
- Ruling: retain the clean existing working checkout and do not create branches or commit the main product without explicit instruction. All changes are scoped and reviewable.
- 2026-11-10: 210 domain checks, 43 IFC tests, 23 native model checks, 10 native regressions, 66 native/IFC solid comparisons passed. All three native files reopen. Hosted inspection and a real AI parameter patch succeeded. Native `_Undo` restored both geometry and authoritative source to revision 2. Integrated IFC export service returned verified results.
- Ruling: preserve manual object styling rather than overwrite it; detect geometry changes separately. Use stable definition/transform fingerprints, not cached instance bounding CRCs. Cap expanded native parts at 5,000 and use process-owned IFC output locks.
- 2026-11-10: warehouse published at `5cdd4205c43d2dff6fe12d9aca530c1256f3e891`; remote ref matches and GitHub run `42652122470` succeeded. Product snapshot pins that commit. Native nested-layer regression is closed with GUID-based layer ownership; final 10/10 regression run is green.
- 2026-11-10: final archives inspected for exact native bytes, required assemblies/worker, descriptor version and source pin. Installed binaries match the packaged candidate. Owned QA process closed. Main product remains uncommitted at `5198dec`, with no release or marketplace update.
