# Native Magpie Implementation Plan

**Goal** Build and publish a Rhino 9 Magpie panel using Magpie routing and Rhino MCP.

**Architecture** A RhinoCommon/Eto plugin hosts the UI. A portable .NET core
implements the gateway and MCP agent loop using the existing Magpie configuration.

**Spec** `docs/superpowers/specs/2026-11-08-native-magpie-design.md`

## Constraints and review focus

Keep the selected group/strategy ID unchanged. Reload credentials for each turn.
Bind MCP calls to the correct document. Serialize tool calls and keep the MCP
result when cancellation occurs. Avoid leaking credentials in HTTP errors.
Preserve existing launcher callers.

## Task 1 — Gateway and MCP agent loop

- [x] Add `src/RhinoMagpie.Agent` with config, gateway, MCP client and tool loop.
- [x] Add focused protocol fixtures for group IDs, multiple tools, errors and cancellation.
- [x] Run the portable agent checks.

## Task 2 — Rhino panel

- [x] Add `src/RhinoMagpie.Plugin` with plugin registration and the `Magpie` command.
- [x] Add document-specific chat, tool approval, cancellation and model selection.
- [x] Build the plugin and run a real MCP modeling check in a test document.

## Task 3 — Distribution

- [x] Update the Magpie descriptor and marketplace package.
- [x] Build a Rhino 9 `.yak` package and add CI/release artifacts.
- [x] Update installation instructions and verification evidence.
- [ ] Commit, push, wait for CI and publish a release.
- [ ] Update marketplace PR and submit Rhino distribution where authenticated.

## Execution

Work continues in this existing dedicated repository under the user's prior
authorization to build, commit and publish. The latest user message accepts the
independent panel fallback, so no further architecture selection is needed.
