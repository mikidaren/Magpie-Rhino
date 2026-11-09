# Local candidate 0.4.0: lightweight OpenBIM

This milestone adds reusable tools rather than another long modeling prompt. The existing Magpie host, native agent-menu compatibility adapter and provider routing remain unchanged.

The shared kernel supports plan axes and space zones, storeys, straight walls, true openings, rectangular shared door/window Blocks, slabs with voids and straight conceptual stairs. Parameters are deliberately architectural and lightweight: location, width/height, thickness, elevation and simple form. No materials or junction build-ups are required. Native geometry stays exact and editable.

The two-storey demonstration uses 12 × 8 m, 240 mm walls, 200 mm slabs, two 3.2 m storeys, ten walls including interior partitions, nine openings, three shared types and an eighteen-riser stair. Four `WIN-A` instances update from 1.2 to 1.5 m width with matching wall voids. Changing only `L2.height` to 3.5 m retains the first storey's object identities and geometry.

`openbim_catalog`, `openbim_apply`, `openbim_inspect` and `openbim_export_ifc` run locally through the existing approval, scale and failure-ledger boundaries. The optional IfcOpenShell worker reopens IFC4 and checks actual OpenCASCADE geometry, spatial containment, types and void/fill links. It does not overwrite outputs. Runtime setup is explicit.

Manual object attributes are preserved. Manual geometry edits, changed units and missing objects are detected before overwrite. Source metadata is undoable and is authoritative on a hidden state object, with a document-string mirror. Shared instance fingerprints use stable type identity and transforms, not a volatile bounding-cache CRC. Generated-part limits protect the UI; crashed IFC workers release OS-owned output locks.

Limitations: this is concept/developed-design OpenBIM, not construction BIM, code checking, structural verification, complete Revit replacement or a full `.gh` visual library. The initial tools do not cover curved walls, arched windows or complex stairs. IFC exchange preserves supported semantic/shape information, not arbitrary native construction histories. Overall token savings have not been benchmarked.

This candidate is built and tested locally. No main-product public release or marketplace update is implied by this document.
