# Magpie-Rhino 0.3.1

Install the Rhino `.yak` package and restart Rhino 9. Open its normal AI panel
and choose **Magpie** alongside Claude and Codex. No command is required.

The product and Magpie marketplace listing are named **Magpie-Rhino**.
The release ZIP includes a same-named companion folder for local Magpie installs,
avoiding the generic `plugin` label. Existing connection settings are preserved.

This release also includes the 0.3 architectural workspace: streamed replies,
reference drawings and scale approval, staged plans with acceptance conditions,
tool/error details, viewport feedback, bounded repair and explicit human review.
Failures remain outstanding until a compatible correction and fresh readback
are explicitly linked. Unrelated successful tools cannot clear a geometry failure.

Local validation: 157 workflow checks, 38 agent checks, 23 legacy core checks,
4 standalone descriptor tests, 5 marketplace tests and registry validation.
Native Rhino testing verified the startup menu entry, switching and return,
and a real Magpie request through the selected native-panel entry.

The native entry is a reversible UI compatibility extension for the tested RhinoAI
version. Architectural quality, long-running projects and other RhinoAI versions
still need further validation. Marketplace approval is managed separately.
