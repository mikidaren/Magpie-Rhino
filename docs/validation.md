# Validation

Initial target: Windows x64, Rhino 9 9.0.26300.09401, Magpie 0.1.3292.

- Agent descriptor tests: passed locally.
- Launcher configuration validation, child environment isolation and Claude
  installation discovery: 23 checks passed.
- Installed RhinoAI inspected read-only: Claude inherits the child environment;
  its model aliases are passed to Claude Code; the Codex config is rewritten.
- Windows self-contained x64 build: passed.
- Fresh-profile installer: passed, including creation of the directory required
  for Magpie to show the agent before a model has been selected.
- Launcher startup command: second desktop launch automatically opened the AI
  panel after preserving the quotes required by Rhino's command-line parser.
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
- Real Magpie disconnect/reconnect: CLI disconnect succeeded, launcher diagnostic
  rejected the disconnected configuration (exit 1), and reconnect restored a
  ready diagnostic (exit 0).
- Initial GitHub Actions run `42482251576`: Windows tests, Linux tests and Windows
  packaging all succeeded. Tag builds publish the release ZIP separately.
- Market review is external to this project; a submitted PR is not acceptance.

The model picker label remains Claude / Opus (latest); this is the agent alias,
not a claim that the upstream model is Claude.
