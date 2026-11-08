# Rhino 9 AI → Magpie

This records the historical v0.1 launcher decision. The current v0.2 design is
`superpowers/specs/2026-11-08-native-magpie-design.md`.

The user requested a working connection from the installed Rhino 9 AI panel to
Magpie, a new GitHub repository with automatic builds, and submission to the
Magpie community market.

## Decision

Use Rhino's built-in Claude agent with a process-scoped gateway environment.
A Magpie agent plugin writes a dedicated JSON file; a small Windows launcher
reads it and starts Rhino. The AI panel stays Rhino's own panel, with its native
MCP tools and permission controls. Claude Code supplies the agent loop; Magpie
supplies the selected model. This integration is not a new native Rhino agent.

Alternative: changing Rhino's Codex config is unreliable because the installed
RhinoAI overwrites it in CodexHome.Prepare. Replacing or patching RhinoAI would
couple distribution to private implementation details. A separate chat panel
would duplicate Rhino's existing integration.

The implementation choice is the maintainer's, not a user-selected architecture.

## Boundaries

- Windows x64, Rhino 9 build 9.0.26300.09401 is the initial verification target.
- Require a native Claude Code installation in a Rhino-supported search path.
- Magpie owns only ~/.config/rhino9-magpie/settings.json bridge keys.
- No editing Rhino binaries, ordinary Claude/Codex profiles, or machine environment.
- The launcher uses a separate Claude config directory and never prints a gateway key.
- Model changes take effect on the next launch; use an alias such as Opus (latest)
  in Rhino's Claude model selector, not a pinned model version.
- Disconnection restores the JSON keys through Magpie; already-running Rhino
  processes retain their inherited environment until closed.
- Local HTTP gateway only; remote credentials are outside this initial release.
- Do not claim market acceptance when only a pull request was submitted.

## Evidence

- McNeel RhinoAI, rhino-9.x: Agents/CodexHome.ai.cs,
  Agents/Conversations/ClaudeStreamJsonParser.ai.cs and Agents/Definitions.json.
- Installed RhinoAI was inspected read-only to confirm those launch behaviors.
- https://usemagpie.ai/docs/plugins#agents
- https://code.claude.com/docs/en/model-config
- https://code.claude.com/docs/en/llm-gateway
