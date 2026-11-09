# Lightweight OpenBIM for Miki-Rhino

Status: user approved the lightweight OpenBIM route and instructed execution on 2026-11-09.

## Intent and scope

Keep Rhino's editable exact geometry and add a small architectural project model, not a full Revit replacement. Concept and developed-design work needs plan position, simple form, wall thickness, height, floor elevation, openings and shared types. Materials, layer build-ups, junction specifications, reinforcement and code certification are outside this pilot.

The first deliverable is a real two-storey example, including native plan curves, walls with through-openings, door/window Block instances, stairs and slabs. Change a shared window type and one storey's height, prove scoped updates, export IFC4, reopen it with IfcOpenShell, and compare actual geometric and semantic results. Only then integrate the tested backend as compact local tools in the existing panel. Preserve the existing Magpie connection and native menu.

## Approach

Use one portable .NET project engine, a RhinoCommon geometry adapter, and a small IfcOpenShell export/readback worker. Store reusable source in `Miki-GrasshopperWarehouse`. Do not fork two plugin identities or introduce FreeCAD/Blender as mandatory hosts. No provider/branding overhaul or donation integration is included in this milestone.

IFC is an exchange representation, not a replacement for the parametric project source. `.3dm` retains native exact geometry, Blocks and the source JSON. IFC types and openings express the same lightweight semantics; they do not promise Revit family history or production BIM completeness.

## Contract version 1

All source lengths are metres, regardless of Rhino document units. Use camelCase JSON and stable ASCII IDs. The root has `schemaVersion: 1`, `id`, `name`, `revision`, `units: "m"`, and the following arrays:

- `levels`: `{id, elevation, height}`.
- `grids`: `{id, start:[x,y], end:[x,y]}`.
- `spaces`: `{id, level, name, boundary:[[x,y],...]}`.
- `walls`: `{id, level, start:[x,y], end:[x,y], thickness, height?}`; omitted height follows the level.
- `slabs`: `{id, level, boundary:[[x,y],...], thickness, offset:0, voids:[[[x,y],...]]}`; slab top is level elevation plus offset.
- `types`: `{id, kind:"window"|"door", form:"rectangular", width, height, depth}`.
- `openings`: `{id, wall, type, offset, sill}`; offset locates the left edge along the wall baseline.
- `stairs`: `{id, from, to, origin:[x,y], direction:[x,y], width, run, risers}`; a straight conceptual solid flight, not a certified stair detail.

No materials, joints or wall build-up are required. Geometry style has internal deterministic defaults, not lengthy model-facing descriptions. A door opening may meet the wall base. IDs are unique across all arrays. Validate finite numbers, positive lengths, sensible bounded counts, nonzero wall/direction vectors, simple polygons, resolvable references, opening fit and overlap, slab void containment, and stair connection geometry before modifying a Rhino document.

`ProjectEngine.Parse(string)`, `Serialize(BuildingProject)`, `Validate(BuildingProject)`, `CreateDemo()`, `ApplyPatch(BuildingProject, JsonObject)`, and `AffectedIds(BuildingProject, BuildingProject)` provide the shared portable interface. Patches merge entries by ID; partial entries update existing elements. The revision increases once per nonempty accepted patch. Do not accept ID changes, unsupported collections, unknown properties or silent deletion. Type changes invalidate only their instances and host openings. Level changes invalidate only dependent walls, instances, spaces, slabs and connecting stairs. No unrelated geometry is regenerated.

## Native adapter

Build RhinoCommon exact curves and closed Breps; no Mesh output. Window/door types use actual shared instance definitions. Resolve wall openings with Boolean subtraction and independently compare resulting volumes. Preserve semantic IDs in object user strings, including project/type/host IDs. Geometry updates preserve object GUIDs where possible. No operation touches geometry from another project or unrelated user objects.

Persist source under `RhinoDoc.Strings` section `MikiOpenBim`, key `project`. Detect missing or manually changed managed geometry before overwriting it. Precompute geometry before applying updates. Changes use the panel's existing undo record; do not nest or clear undo. Failed operations remain explicit and use the existing failure ledger. Units and tolerance are never silently changed. Undo/readback restores or invalidates project state rather than pretending geometry and metadata remain aligned.

## IFC worker

`python python/miki_ifc.py export --project SOURCE --output MODEL.ifc --report REPORT.json` writes IFC4 and reopens the file. Storeys, spaces, walls, slabs, stairs, opening voids, door/window filling relations and shared types retain stable identity. Shapes use exact swept solids and mapped type representations rather than mesh proxies. Readback verifies entity relations, type sharing, actual OpenCASCADE geometry, bounds and volume against source-derived expectations. Reject invalid exports; never report file existence alone as verification.

Pin the worker dependency version. Runtime setup is explicit and local; no plugin auto-installation, arbitrary remote script execution, or runtime `latest` download. A missing IFC runtime disables export with actionable setup guidance but does not disable native modeling. Export paths are user-selected or explicitly supplied and writes go through tool approval.

## Compact integration

Use optional `ILocalModelingTools` in the portable agent. Definitions have the normal MCP `{name, description, inputSchema, annotations}` shape; execution returns MCP content plus `isError`. Register `openbim_catalog`, `openbim_apply`, `openbim_inspect`, and `openbim_export_ifc`. `openbim_apply` receives an explicit project or patch; never silently build the demo in a user's document. A catalog/describe response exposes only needed parameter information. Source code and full native geometry never enter LLM history.

The native backend is document-scoped, uses the existing approval/scale/undo/cancellation rules and returns compact counts, revisions, affected IDs and checks. Successful `openbim_inspect` is a real document readback and may satisfy freshness. File export is not a geometry repair. Unknown outcomes latch the existing fail-closed state. YOLO does not grant installation, scale changes or unrequested overwrite permission.

## Evidence and acceptance

- Start an owned hidden Rhino process and a new empty document; never modify the user's open unsaved model.
- Save before/after `.3dm`, before/after IFC, JSON source and machine-readable validation.
- Prove exact native object kinds, valid closed wall solids, real through-openings and both floor plans.
- Change `WIN-A.width` from 1.2 to 1.5 m; prove every associated Block updates, unchanged type/other geometry stays unchanged, and host voids update.
- Change only `L2.height` from 3.2 to 3.5 m; prove L1 walls retain bounds and GUIDs.
- Reopen both `.3dm` and IFC; verify identity, quantities, storeys, types, void/fill links, geometry and bounds, not just names.
- Record process ownership, tests, results and limitations. No claim of structural/code compliance, complete BIM authoring, or measured token percentage savings.

## Delivery boundaries

Source and generic demo may be published to the requested warehouse repository after validation. User model/history and machine paths/secrets stay local. Existing product changes remain a local candidate unless a release is explicitly requested. The warehouse begins with native parametric kernels usable from Rhino/Grasshopper; do not claim that a compiled kernel is a complete visual `.gh` document.
