# Validation

## Version 0.3.4 exact-geometry defaults

Verified on November 9, 2026.

- The default instructions prefer NURBS-compatible profiles and exact surfaces,
  with native Extrusion or closed Brep representations for volumetric parts.
  Explicit Mesh/SubD requests remain supported.
- Per-request document context includes geometry-type counts and absolute
  tolerance in metres. A normal native-panel request supplied this context.
- A live test in an isolated metre-based document created two exact ArcCurve
  profiles, one open Brep canopy surface and one closed Brep column. All four
  objects were valid. Profile crown points met the 0.001 m tolerance, and column
  volume matched 0.27 cubic metres.
- Two default-instruction regressions were added. Agent checks passed at 45,
  and workflow checks passed at 157. The native Release build had no compiler
  errors or warnings, and installed/plugin build hashes matched.

The defaults guide agent decisions. Scope-specific geometry readback supplies
the acceptance evidence for each generated model.

## Version 0.3.3 remembered YOLO and streamed-detail scrolling

Verified on November 9, 2026.

- YOLO preferences persist in a separate `panel-preferences.json`. New profiles
  default to confirmation; new conversations and document changes retain the
  choice. Saving does not modify gateway credentials.
- Native test restored YOLO in a new Rhino process and a new panel. A real
  `run_csharp` call completed without a tool-confirmation dialog and printed
  `YOLO_MODE_OK 11`, without changing geometry.
- Two-panel review reproduced stale automatic permission after revocation.
  Live panels now synchronize execution mode, and each tool reads the current
  permission. Runs reload the stored preference before dispatch.
- A second reproduced race involved unrelated stale options re-enabling YOLO.
  Permission changes now use a dedicated action and revision; architecture and
  detail updates contain no permission field.
- Streaming browser regressions originally reproduced six failures, including
  expansion jumping from scrollTop 2073 to 2401. Trusted scroll intent now
  controls following, and native anchoring preserves the reading position.
- Browser cases cover detail expansion, nested wheel boundaries, stream growth,
  resizing, scrollbar dragging, delayed images and text selection. They also
  verify isolated execution-mode messages and permission revisions.
- Embedded Rhino WebView trusted input verified streaming position remained
  2286 to 2286 and nested bottom-wheel input left the outer position 2482 to 2482.
- Native two-panel tests verified revocation in the other panel and rejection
  of both unrelated stale options and stale execution-mode revisions.
- Agent checks: 43 passed; workflow checks: 157 passed; browser checks: 20 passed.
  Browser checks are
  included as a required CI job before packaging.
- The marketplace Agents table uses `rhino9-ai` as its package slug while
  retaining the product display name Magpie-Rhino.

## Version 0.3.2 automatic model catalog

Verified on November 9, 2026.

- Reproduced the reported defect in installed 0.3.1: opening the panel, without
  clicking Refresh, left only one placeholder choice and zero routing groups.
- The same authenticated gateway returned 484 eligible entries, including 80
  routing groups. All 80 groups advertised tool-call support.
- After installing 0.3.2 in an owned Rhino test process, opening the panel
  automatically loaded 485 choices including the follow entry and all 80 groups.
- Native DOM readback confirmed 485 rendered options, the displayed group/entry
  counts, the `group/smart-split` option, and the preserved default selection.
- The JavaScript syntax check, 4 descriptor tests and 38 agent checks passed.
  Release compilation completed with zero errors or warnings.
- The user's already-open Rhino process was preserved. Installed DLL and built
  DLL SHA256 hashes matched; a Rhino restart loads the new plugin.

## Version 0.3.1 Magpie-Rhino release

Verified on November 9, 2026, with installed Magpie 0.1.3323 and Rhino 9
9.0.26300.09401 on Windows x64.

- Product, marketplace registry name, Agent display name, Rhino metadata and
  companion folder are named **Magpie-Rhino**. Existing `rhino9-ai` and
  `rhino-magpie` technical IDs are retained for connection and Yak compatibility.
- A new Rhino process loaded package 0.3.1 at startup. Opening only Rhino's normal
  `AI` panel displayed **Claude**, **Codex** and **Magpie** in the same native
  agent menu. No `Magpie` command was issued.
- Clicking the injected Magpie row after the native undo record completed
  opened the hosted Magpie panel. The return action restored the original native
  WebView and its agents. A click during undo recording did not switch.
- A request through the hosted panel called `get_context` and returned
  `NATIVE_AI_MAGPIE_OK` with 11 objects. It made no geometry changes.
- Tests passed: 157 workflow checks, 38 agent checks, 23 legacy core checks,
  4 standalone descriptor tests, 5 marketplace package tests and registry
  validation. The isolated installer regression passed.
- The native entry was also checked in a browser fixture (25 checks) and source
  checks (9). Independent source/artifact review found no actionable P1/P2 issue.
- The Release build completed with zero compiler errors or warnings. Yak reports
  the historical assembly-name/package-name difference as a cosmetic warning.

This remains a compatibility UI extension over the known RhinoAI page structure,
not a new registered backend runner. Rapid document-close races, other RhinoAI
UI versions and complex architectural production cases require further testing.
Public build, release and marketplace acceptance are separate observable states.

## Version 0.3.0 local architectural workspace

Verified locally on November 9, 2026. Publication remains deferred.

- 157 workflow checks and 38 agent checks passed. Coverage includes
  fragmented streaming text and tool calls, truncation refusal, compiler-error
  repair, explicit scale approval, preserved pending scope, failed-change
  readback invalidation, per-reference units and human review. Legacy core checks
  passed (23), descriptor tests passed (3), and the isolated installer passed.
- Regression tests reproduced unrelated material success masking a geometry
  failure before the fix. The operation ledger now retains each failure until
  explicit resolution links a compatible successful correction, a current-run
  readback after the latest mutation, and a verification note. Tests reject
  wrong families, wrong stages, stale and previous-run readbacks, and independent
  failures left unresolved. Ledger IDs survive native output truncation above 64K.
- The native plugin builds with zero compiler errors or warnings.
- Isolated browser checks cover draft/selection/scroll preservation, document
  identity changes, invalidated width dialogs, delayed Eto bridge injection,
  persistent plan visibility and safe text rendering.
- An actual embedded WebView in Rhino reported both `window.eto` and
  `window.magpie`. Its posted actions changed options and imported the selected
  synthetic drawing. A native browser capture verified the rendered interface.
- A synthetic 12 by 8 metre drawing was imported into a millimetre document.
  These dimensions are test assumptions, with no real project supplied.
- The architectural session created a plan for scale, structure, cornice detail
  and final checks. It proceeded beyond the six massing objects to create the
  four independently required cornices.
- Independent Rhino readback confirmed ten valid, solid native Breps plus the
  retained reference curve. All four plan stages were checked. The final state
  was `awaitingReview=true`, `accepted=false`.
- Partial assistant text was recorded while the native model request was still
  running, proving actual streaming delivery rather than a delayed text replay.
- A live C# test intentionally compiled the user's error types. Rhino returned
  a compiler failure. The agent corrected `LayerTable.FindName` handling, the
  reserved `base` parameter and the top-level return, then ran the corrected
  script and reported `CSHARP_REPAIR_OK` without changing model objects.
- A repeated live repair test against the ledger build linked failed `op-2`,
  corrective `op-3` and readback `op-4` through `resolve_modeling_failure`.
  Independent host inspection confirmed `Outcome=complete` in direct mode and
  `HasUnresolvedFailures=false`; the document retained 11 objects and millimetres.
- Native AI compatibility hosting was tested in the installed
  `Rhino.AI.UI.AIPanel`. The added selector contained **Rhino 内置代理** and
  **Magpie**. The ordinary `Magpie` command selected the hosted Magpie UI.
- Switching to native mode restored its original WebView. Switching back reused
  the Magpie instance. A real request through that hosted UI called `get_context`
  and replied `NATIVE_AI_MAGPIE_OK` with 11 objects.
- Importing an image with a mismatched unit token was refused with no geometry
  changes. Valid import measured 6000 document millimetres in width.
- Native hosting retains one adapter per panel/document, defers selection beyond
  the opening command's undo record and restores or disposes the original
  WebView according to its owner lifetime.

The compatibility selector hosts our agent in the native UI through public Eto
control APIs. It does not register a fourth RhinoAI backend runner or replace
RhinoAI files. Compatibility with other RhinoAI UI versions, extensive multi-window
workflows, persistent project history and complex architectural/Grasshopper
production cases require further verification.

## Version 0.2.1 local candidate

This candidate remains local pending the user's decision to upload.

- 37 portable agent checks passed, including startup directory creation,
  settings preservation, malformed model responses, text blocks, MCP transport
  uncertainty, completed RPC errors and question parsing.
- Standalone descriptor checks passed, 3 tests. Existing legacy checks passed,
  23 checks and the isolated installer regression.
- Community package checks passed, 5 tests, plus registry validation. The local
  community package has the correct trusted-publishing repository metadata,
  install command and ordering after the other agents.
- Native plugin built with zero compiler errors or warnings.
- Installed the `.yak` directly with Rhino's Yak tool. The existing connection
  directory was temporarily moved to a verified sibling backup. Ordinary Rhino
  startup, without the installer script or the Magpie command, created the empty
  detection directory. The original settings were restored byte-for-byte.
- The installed plugin reported `LoadTime=AtStartup`, version `0.2.1.0`.
- Native question dialogs rendered radio buttons and checkboxes, rejected an
  unanswered submission, returned selected answers and supplementary text, and
  handled cancellation. Tool confirmation returned false for reject and true
  for allow.
- Document draft isolation and automatic-approval reset passed native component
  checks. Refresh preserved the selected routing group, could be stopped, and
  restored enabled controls afterward.
- A real panel session using `group/smart-split` created a temporary test pavilion
  with six valid closed native Breps. Test assumptions were 6000 by 4000 mm in
  plan, four 200 by 200 mm columns, and a roof at z=3000 to 3200 mm.
- The model read back bounding boxes, solidity and volumes, then received a
  native viewport image and completed its response.
- A second turn changed only the roof thickness to 300 mm. Independent native
  readback confirmed the roof's lower face stayed at z=3000 and the other five
  objects retained their IDs and bounding boxes.
- A separate isolated panel session created a radius-20 sphere and changed it
  to radius 30 on its next turn. Two ordinary Rhino `_Undo` commands reverted
  the edit and then the creation, restoring zero live objects.
- An earlier test called `doc.Undo()` from inside an MCP C# script and damaged
  the test undo sequence. The same effect reproduced with ordinary Rhino spheres
  without the plugin. The final acceptance check uses native Undo commands.
- Stopping a model request restored controls and closed the turn's undo record.
  A subsequent read-only panel request succeeded with `RECOVERY_OK`, confirming
  the panel remained usable.
- Disposing the panel during a pending refresh completed without closing Rhino
  or breaking its native MCP listener.
- Independent read-only review of this candidate found no important or critical
  defects in the scoped changes.

Grasshopper workflows, very large or long-running modeling tasks and every
provider combination remain outside this verification.

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
