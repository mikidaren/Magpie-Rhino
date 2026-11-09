# Magpie-Rhino 0.3.4

Default modeling instructions now use an editable exact-geometry route from
profile curves to NURBS-compatible surfaces and native Extrusion/Brep solids.
Lines, arcs, planes and revolved surfaces retain their exact representation.
The user can explicitly request Mesh/SubD output.

The agent receives current geometry-type counts and the document's absolute
tolerance expressed in metres. Instructions cover useful construction curves,
stable part references, repeated components, real wall openings and stage-focused
verification. Recent inspection results can be reused between relevant edits.

These changes guide the agent's geometry and tool choices. Geometric results
continue through Rhino readback and the existing human-review workflow.
