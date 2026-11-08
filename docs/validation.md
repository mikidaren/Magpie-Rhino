# Validation

Initial target: Windows x64, Rhino 9 9.0.26300.09401, Magpie 0.1.3292.

- Agent descriptor tests: passed locally.
- Launcher configuration validation, child environment isolation and Claude
  installation discovery: 23 checks passed.
- Installed RhinoAI inspected read-only: Claude inherits the child environment;
  its model aliases are passed to Claude Code; the Codex config is rewritten.
- Windows self-contained x64 build: passed.
- Gateway diagnostic: Magpie identity and selected catalog model passed.
- Actual desktop test on November 8, 2026: Rhino's native Claude command submitted
  a read-only request from an empty unsaved document. The native AI panel displayed
  a `get_context` call and replied `RHINO_MAGPIE_OK 0`.
- Magpie's local usage records independently showed two HTTP 200 requests under
  agent `rhino9-ai`, provider `chatgpt-api`, model `gpt-6.1-sol`.
- Claude Code native 2.1.311 was installed for the verification.
- Geometry creation, multi-turn modelling, other providers and other operating
  systems have not been verified.
- The native floating WebView rejected automated mouse input; the test used
  Rhino's built-in `Claude` command, which submits into the same AI panel.
- Market review and hosted CI results are reported in the repository/PR checks.

The model picker label remains Claude / Opus (latest); this is the agent alias,
not a claim that the upstream model is Claude.
