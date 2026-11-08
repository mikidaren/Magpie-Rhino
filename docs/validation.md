# Validation

## Version 0.2.0 native panel

Verified on November 8, 2026 with Rhino 9 `9.0.26300.09401`, Windows x64 and
Magpie `0.1.3292`.

- Portable agent protocol checks passed, 19 checks. Coverage includes unchanged
  group/strategy IDs, credential rotation, consecutive tool responses before
  image feedback, approvals, cancellation and blocked retries after an uncertain
  tool completion.
- Existing descriptor checks passed, 3 tests. Legacy launcher checks passed,
  23 checks, and its isolated installer regression passed.
- Magpie marketplace package checks passed, 4 tests and registry validation.
- Native `.rhp` build passed with zero compiler errors or warnings.
- Official Yak builder generated `rhino-magpie-0.2.0-rh9_0-win.yak`.
  The builder reports cosmetic assembly name/version metadata differences.
- The ZIP installer successfully installed the package into Rhino's ordinary
  user package directory. A fresh, ordinary Rhino process automatically discovered
  the plugin and the `Magpie` command opened its native panel.
- The installed panel refreshed 463 tool-capable model/group choices from Magpie.
- The installed panel completed a read-only request and displayed
  `MAGPIE_PANEL_OK 0` after calling `get_context`.
- An agent-loop test using `group/smart-split` against a separate empty document
  created `MAGPIE_NATIVE_SMOKE`, a native solid Brep sphere of radius 5 with
  volume `523.59877559980919` in the existing millimetre units.
- A second test used the installed panel's normal Send event, with the custom
  group selected in its model picker and automatic tool approval enabled for
  that test. It called `get_context`, `run_python` and geometry readback, created
  `MAGPIE_PANEL_SMOKE`, and reported `PANEL_MODEL_OK`. The native Brep was valid
  and solid with volume `113.09733552955873` and radius 3.
- Undo returned true and `list_objects` confirmed zero live objects afterward.
  Rhino's raw ObjectTable.Count retains deleted objects for undo, so it was not
  used as the final live-object check.
- Test automation invoked the public Eto button event through Rhino's script
  interface after Windows input could not reliably address the floating window.
- Independent read-only review identified a shared timeout and missing undo
  ownership check. Both were fixed, three checks were added, and targeted
  re-review reported no remaining important defects in those fixes.
- The installed Magpie companion descriptor was updated locally to 0.2.0.
- Rhino Yak upload was attempted but rejected because there was no cached
  Rhino Account token. `yak login` opened the account authorization page.
- Windows browser automation stopped because it could not reliably determine
  the current browser URL. Rhino account authorization and Food4Rhino submission
  remain pending. No Rhino marketplace acceptance is claimed.
- GitHub release and Magpie marketplace revision are recorded in the associated
  Git history and release/PR. CI status must be checked against the published commit.

Complex building tasks, all providers, and visual parity with the built-in agent
have not been validated.

## Version 0.1 historical launcher evidence

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
