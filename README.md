# Rhino 9 + Magpie

Use a model selected in **Magpie** inside **Rhino 9's existing AI panel** on
Windows. Includes a Magpie agent plugin and a self-contained Windows launcher.

```text
Rhino 9 AI panel → built-in Claude Code agent → local Magpie gateway → selected model
```

This is an unofficial integration. The Rhino panel still calls its agent
**Claude**; the model is supplied by Magpie. Claude Code is required as the agent
runtime, and a separate Claude subscription is not required for gateway access.
Magpie's upstream accounts, availability and charges still apply. Other model
families depend on Magpie's protocol translation and tool-call compatibility;
this project does not guarantee every model works equally well.

## Install

1. Install Rhino 9, Magpie, and native Claude Code using
   [Anthropic's Windows instructions](https://code.claude.com/docs/en/setup).
   The launcher recognizes `%USERPROFILE%\.local\bin\claude.exe`, the npm
   installation in `%APPDATA%\npm`, the Store runtime, and Claude Desktop's
   standard and `Claude-Data` layouts.
2. Download `RhinoMagpie-win-x64.zip` from
   [Releases](https://github.com/mikidaren/rhino9-magpie/releases), then extract it.
3. Run the extracted `install.ps1` first. It creates the **Rhino 9 + Magpie**
   desktop shortcut and the directory Magpie uses to detect this integration.
4. Install the included `plugin` folder from Magpie's Plugins page, or run
   `magpie plugin add "C:\path\to\extracted\plugin"`. The source checkout's
   `plugin` folder also works. Keep that folder at a stable location.
5. In Magpie → Agents, find **Rhino 9 AI** and choose a model.
6. Open the desktop shortcut, or run `RhinoMagpie.exe` directly.
7. In Rhino's AI settings, select **Claude** and **Opus (latest)**, then start a
   new conversation in the AI panel.

The npm package name is reserved for the community-market submission; do not
assume it is published until the market maintainers publish it. The bundled local
plugin is installable independently of that review.

## 中文使用说明

这是将 **Rhino 9 原生 AI 面板接入 Magpie** 的 Windows 工具。
在 Magpie 里选择模型，用专用启动器打开 Rhino，然后在 Rhino 的 AI 面板中选择
**Claude → Opus (latest)**。面板名称仍是 Claude，但请求经 Magpie 发送给所选模型。
Claude Code 在这里负责调用 Rhino 工具，模型服务由 Magpie 提供。

先安装原生 Claude Code；解压 Release 文件，先运行 `install.ps1`
创建快捷方式和用于检测的配置目录，再在 Magpie 安装其中的 `plugin`
文件夹，在 Agent 页的 **Rhino 9 AI** 行选择模型。最后通过快捷方式或
`RhinoMagpie.exe` 打开 Rhino。

切换 Magpie 模型后，请关闭这次通过启动器打开的 Rhino，再用启动器打开。
Rhino 模型栏请选择 `Opus (latest)`、`Sonnet (latest)` 或 `Haiku (latest)`
之一；固定版本名称不会使用这些别名映射。
普通 Rhino 快捷方式不注入本工具的连接设置。

## How isolation works

Magpie manages only `model`, `baseUrl`, and `apiKey` inside
`~/.config/rhino9-magpie/settings.json`. Its normal agent-disconnect flow restores
those keys. This file contains a gateway credential: do not share or commit it.

The launcher reads that file, checks Magpie's identity and model catalog, finds
Rhino 9 through the Windows registry, and starts a new Rhino process with gateway
environment variables. It maps Opus, Sonnet and Haiku aliases to the selected
model and uses `%LOCALAPPDATA%\Rhino9Magpie\Claude` for isolated Claude data.
It does not modify your normal Claude/Codex settings, Rhino binaries, model files,
or machine/user environment variables. Rhino continues managing its MCP tools.

Only a local HTTP Magpie gateway is supported in this initial version. Remote
gateways and non-default bridge config locations need an explicit implementation.
`--config path` supports an explicitly selected alternate bridge JSON.

Changing or disconnecting the agent affects the next launch. An already-running
Rhino retains its process environment until closed. Disconnect in Magpie before
uninstalling the plugin. `install.ps1 -Uninstall` removes the desktop shortcut;
it keeps launch files and session data to avoid deleting your work.

## Diagnose

```powershell
.\RhinoMagpie.exe --diagnose
.\RhinoMagpie.exe --rhino "E:\Apps\Rhino9\System\Rhino.exe"
```

When collecting output from this Windows GUI executable, use `Start-Process
-Wait -PassThru -RedirectStandardOutput diagnosis.json -WindowStyle Hidden`, or
invoke it from a process that captures stdout. Diagnostic output reports paths,
model and gateway address, never the credential. Diagnostics query only the
gateway catalog; they do not send a model request.

If the AI panel asks for sign-in, verify that Rhino was opened by this launcher
and that Claude Code is installed in one of Rhino's supported locations.
If a specific model fails on tool use, choose another tool-capable model in
Magpie and restart the launched Rhino.

## Build and verification

```powershell
node --test tests/agent.test.mjs
dotnet run --project tests/Core.Tests --configuration Release
dotnet publish src/RhinoMagpie -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/launcher
```

GitHub Actions runs tests on Windows and Linux, builds the Windows x64 launcher,
and uploads a ZIP with SHA-256 checksums. Pushing a `v*` tag publishes a Release.
The tests require Node 22+ and .NET 8; the packaged launcher includes its runtime.
CI cannot exercise a licensed Rhino desktop or your private Magpie accounts.

Initial compatibility target: Rhino 9 `9.0.26300.09401` and Magpie `0.1.3292`.
See [validation](docs/validation.md) for the distinction between automated,
gateway and desktop verification.

## Sources

- [Rhino AI command](https://docs.mcneel.com/rhino/9/help/en-us/commands/ai.htm)
- [McNeel RhinoAI source](https://github.com/mcneel/RhinoAI)
- [Magpie agent plugin contract](https://usemagpie.ai/docs/plugins#agents)
- [Claude model aliases](https://code.claude.com/docs/en/model-config)
- [Claude gateway setup](https://code.claude.com/docs/en/llm-gateway)

MIT. No Rhino, Claude Code, or Magpie binaries are redistributed.
