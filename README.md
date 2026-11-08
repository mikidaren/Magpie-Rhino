# RhinoMagpie

A native **Magpie** modeling panel for **Rhino 9 on Windows**, using your local
Magpie gateway, model groups and routing strategies.

```text
Rhino Magpie panel → selected Magpie model / group → tool calls → Rhino MCP → native geometry
```

The panel supports separate conversations per document, model and group selection,
tool approvals, cancellation, and viewport image feedback. Magpie owns model
routing and provider accounts. Rhino's installed AI plugin supplies the MCP tools.

## 安装和使用

1. 下载 Release 中的 `RhinoMagpie-panel-win-x64.zip` 并完整解压。
2. 运行其中的 `install-panel.ps1`，安装 Rhino `.yak` 包，然后重启 Rhino 9。
3. 在 Magpie 安装配套连接插件。市场审核期间可使用本机路径安装
   `magpie plugin add "完整路径/plugin"`。
4. 在 Magpie 的 Agent 页连接 **Rhino 9 · Magpie**，选择模型、自定义组或策略。
5. 正常启动 Rhino，在命令行输入 `Magpie` 打开面板。
6. 输入建模要求，点击发送。支持 `Ctrl+Enter`。默认会在执行修改前显示工具参数供确认。

面板默认选择 **跟随 Magpie 当前模型 / 自定义组**，每次发送重新读取连接配置。
点击刷新模型可读取 Magpie 的完整模型及组列表，也可在面板内临时选择。
自定义组和 `auto/...` 策略的 ID 原样传给 Magpie，路由由 Magpie 执行。

可主动勾选 **本次会话自动执行修改**。停止后会等待已发出的 Rhino 操作返回，
再结束本轮，继续之前请检查模型。切换或关闭文档会停止当前回合。
每轮修改由 Rhino 撤销记录包围，可使用 `Ctrl+Z`。

## Requirements

- Windows x64, Rhino 9 with its built-in RhinoAI plugin enabled, and Magpie.
- A Magpie model or routing group with tool-call support.
- A local HTTP Magpie gateway. The connection file must contain a gateway root
  URL without `/v1`.

The plugin opens a separate native dockable panel named **Magpie**. The installed
RhinoAI agent runner currently supports Claude, Codex and Gemini through an
internal registry. The independent panel is the accepted integration fallback.
Its modeling agent runs directly against Magpie.

The earlier v0.1 launcher remains under `src/RhinoMagpie` as a legacy component.
Use the v0.2 panel package for the direct integration described here.

## Configuration and document scope

The Magpie descriptor manages only `model`, `baseUrl` and `apiKey` in
`~/.config/rhino9-magpie/settings.json`. Disconnect in Magpie restores those keys.
The file contains a local gateway credential. Keep it private.

The panel reuses the MCP endpoint belonging to the active document. Listener
lookup uses a read-only reflection call to the installed RhinoAI host because
that version exposes no public per-document port API. When unavailable, the
panel starts a document listener using Rhino's `MCPStart` command.

Tool results and conversations stay in memory until the panel closes. Prompts,
relevant model state and viewport images requested by the agent pass through
Magpie to the selected provider. Model compatibility and modeling quality vary.
Inspect created geometry and review any script or file-write approval.

## Build and checks

```powershell
node --test tests/agent.test.mjs
dotnet run --project tests/Core.Tests -c Release
dotnet run --project tests/Agent.Tests -c Release
dotnet build src/RhinoMagpie.Plugin -c Release
./scripts/build-panel.ps1 -YakPath "E:/WORK/Rhino9/System/Yak.exe"
```

The Yak builder can also be downloaded from the official McNeel CLI download
page. CI uses the pinned Windows x64 Yak 0.19.1 builder and produces the `.yak`,
installer ZIP and SHA-256 checksums. GitHub tags publish release artifacts.

## Distribution

Magpie marketplace submission is tracked in `magpie-community/plugins#868`.
Rhino PackageManager/Yak and Food4Rhino are separate distribution channels.
Their actual submission state is recorded in `docs/validation.md`.

MIT. Unofficial community integration by mikidaren.
