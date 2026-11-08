# RhinoMagpie native plugin

## User intent

The user prefers Magpie in Rhino's native AI agent selector and explicitly accepts
a separate native Magpie panel if that integration is difficult. The panel must
use Magpie model groups and routing strategies and drive Rhino MCP for modeling.
Publishing includes GitHub builds, the Magpie community marketplace, and Rhino
distribution submission.

## Architecture decision

Use a dockable Eto panel in a RhinoCommon `.rhp` plugin. The installed RhinoAI
runner switch supports only Claude, Codex, and Gemini. Its agent registry is
internal and supplies no public registration method. The SDK HTTP definitions
found elsewhere in the source do not establish an installed panel extension point.
A companion panel is the user's accepted fallback and is the implementation choice.

The panel reads Magpie's existing `~/.config/rhino9-magpie/settings.json` on every
turn. It sends the selected model ID unchanged to the local gateway's
OpenAI-compatible chat completions endpoint. Group IDs and strategy aliases are
resolved by Magpie. A model picker can override the selection for the current
panel session without changing Magpie's configuration.

An agent loop lists tools and calls them over the installed RhinoAI MCP HTTP
endpoint bound to the current document. It reuses that document's listener where
available and starts one with Rhino's MCPStart command when required. Read-only
reflection is limited to looking up the existing document listener port because
RhinoAI currently exposes no public discovery API for an in-process plugin.
Failure falls back to the public command. Changes to the active document stop the
current turn. The panel retains separate conversations per document.

## Behavior

- `Magpie` opens the panel from an ordinary Rhino launch.
- Chat, model/group selection, refresh, stop, and new conversation are available.
- Model output and tool activity appear in the panel.
- Mutating tools require approval by default. An explicit session toggle permits
  automatic tool execution. Internal router control and file-close tools are excluded.
- User questions use a panel dialog. Image tool results are forwarded as multimodal
  messages so supported models can inspect the viewport.
- Conversation messages are kept in memory. Secrets and full prompts are not logged.
- Tool execution is serialized. Cancellation after dispatch is reported honestly
  because an already-running Rhino command may complete.
- Native tool undo behavior is checked in Rhino. No claim of complete parity with
  Claude or every available model is made from one test.

## Distribution and checks

Version 0.2.0 replaces the launcher-first instructions. The previous launcher
remains available as a legacy component. The Magpie descriptor connects the same
configuration file to the new panel. A `.yak` package supports Rhino 9 on Windows.
GitHub Actions builds, tests, and attaches the plugin package to a release.

Verification covers config reload and aliases, agent tool loop and protocol errors,
MCP errors, cancellation, plugin build and loading, and a real model creating and
querying native Rhino geometry in a separate test document.

Yak publication uses an existing Rhino Account session if available. Food4Rhino
submission depends on its account and required listing metadata. Submission and
acceptance are recorded separately.
