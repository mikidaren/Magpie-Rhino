# Magpie-Rhino

The local 0.4.1 candidate includes a private offline IFC runtime and lightweight OpenBIM tools.
It retains editable exact curves, NURBS-compatible surfaces and native solids.
Magpie appears directly in Rhino's native AI agent menu.

**Public binary availability:** the latest published Release is 0.3.4. The 0.4.1
source in this branch is under development; its full offline IFC binary is not yet
published while third-party source and redistribution details are reviewed.
The 0.3.4 download does not contain the 0.4.1 OpenBIM and offline IFC features.

![Magpie in the native Rhino AI agent menu](docs/native-ai-menu.png)

A **Magpie** architectural modeling workspace for **Rhino 9 on Windows**, using your local
Magpie gateway, model groups and routing strategies.

```text
Rhino Magpie panel → selected Magpie model / group → tool calls → Rhino MCP → native geometry
```

The panel supports separate conversations per document, model and group selection,
tool approvals, cancellation, and viewport image feedback. Magpie owns model
routing and provider accounts. Rhino's installed AI plugin supplies the MCP tools.

The architectural workspace adds live streaming, a task plan with acceptance
conditions, drawing references, scale grounding, repair attempts and human review.
Architectural mode is enabled initially; direct editing remains available in settings.

The agent starts with profile curves, builds exact surfaces, and forms Extrusion
or closed Brep solids for volumetric parts. Native lines, arcs, planes and
revolved surfaces retain their exact representation. Useful construction curves
follow the requested output scope. Explicit Mesh/SubD requests remain supported.
Each request supplies current geometry-type counts and absolute tolerance in
metres. Verification focuses on the active stage and the edited parts.

## 简易 OpenBIM：方案与深化

这一版本把可复用建模工具接入了面板，不只是增加提示词。项目记录楼层标高和高度、
轴网与空间分区、墙线和厚度、门窗位置及简单类型、楼板洞口、直跑楼梯。
不要求材料层、墙窗节点、连接构造或施工详图参数；门窗深度可省略，内部默认 0.12 m。
Rhino 中仍然是精确曲线和 Brep 实体；同型门窗使用真实共享 Block。

四个本地工具负责查询参数、应用小补丁、读回检查和 IFC 导出：
`openbim_catalog`、`openbim_apply`、`openbim_inspect`、`openbim_export_ifc`。
例如改窗宽只需要 `{"types":[{"id":"WIN-A","width":1.5}]}`，
不必重新生成或反复传送建模脚本。窗口实例、共享定义和相关墙洞一起更新。
补丁携带当前版本，不能用旧版本覆盖新项目；手动几何修改会被检测，
手动图层、颜色、名称、分组和自定义属性保留。

工具源位于 [Miki-GrasshopperWarehouse](https://github.com/mikidaren/Miki-GrasshopperWarehouse)。
当前产品使用 `vendor/openbim/snapshot.json` 记录的固定源码及 SHA256，运行中不下载代码。
这是原生参数化内核，不是完整的可视化 `.gh` 定义库或 Revit 替代品。
当前支持直墙、矩形门窗、直跑概念楼梯；曲墙、异形窗和施工级 BIM 尚未覆盖。
首版限制为估算 5,000 个生成部分，防止超大输入卡住 Rhino。

### 内置离线 IFC

完整 0.4.1 包自带私有 CPython 3.12.13、IfcOpenShell 0.9.0.post1 和固定依赖。
用户无需安装 Python、运行 pip 或配置路径。解释器不加载进 Rhino，而是在独立后台进程中执行。
插件只解析自身 `openbim/runtime`，不会回退到系统 Python、开发环境或旧连接配置。
启动使用隔离模式，禁止用户模块路径、site 初始化和字节码写入，不继承模型服务的环境凭据。

在面板设置中选择 **导出 IFC**，即可直接选择目标文件并读回验证，不调用模型、消耗模型 tokens
或要求 Magpie 网关可用。仍可让 AI 调用同一个本地导出工具。
IFC 导出与核对可离线执行；云模型对话仍需要相应网络与服务。

内置组件及导出脚本有文件清单与 SHA256 检查；缺失或损坏会提示重新安装完整包，
不会临时下载依赖或让用户手动修环境。导出不会覆盖已有文件。
它会重新读取 IFC4，核对实际几何体积、尺寸、楼层、共享类型和洞口关联，
不以“文件存在”代替验证。IFC 不保证 Revit 族或 Grasshopper 参数历史往返。
`.3dm` 内保存原生几何、共享 Block 及简易项目源状态。

当前完整包仅供本机验证：捆绑的原生组件涉及 CGAL 等多种许可证，
`openbim/runtime/licenses/PUBLIC-RELEASE-GATE.md` 明确列出未闭环项。
材料收集不等于法律合规审定，公开二进制上传与 Release 在审阅完成前被构建流程阻止。
此限制不冒充 Python 安装问题，也不会把开发环境路径写成用户配置。

开发构建见 `docs/superpowers/specs/2026-11-10-offline-ifc-runtime-design.md`。
构建期源码证据与依赖下载需要显式选项，最终用户运行时不下载代码或依赖。

## 安装和使用

1. 下载 Release 中的 `Magpie-Rhino-win-x64.zip` 并完整解压。
2. 安装其中的 `.yak` 包，或运行 `install-panel.ps1`。然后重启 Rhino 9。
   插件会在启动时自动创建检测目录，不改写已有模型和连接配置。
3. 在 Magpie 安装配套连接插件。市场审核期间可使用本机路径安装
   `magpie plugin add "完整路径/Magpie-Rhino"`。
4. 在 Magpie 的 Agent 页连接 **Magpie-Rhino**，选择模型、自定义组或策略。
5. 正常打开 Rhino 的 **AI 面板**，在代理菜单选择 **Magpie**，无需输入命令。
   `MagpieStandalone` 提供独立面板入口。
6. 输入建模要求，点击发送。`Enter` 发送，`Shift+Enter` 换行。默认会在执行修改前显示工具参数供确认。

面板默认选择 **跟随 Magpie 当前模型 / 自定义组**，每次发送重新读取连接配置。
点击刷新模型可读取 Magpie 的完整模型及组列表，也可在面板内临时选择。
列表会在面板启动时自动加载，打开下拉框时按需更新；下拉框底部显示路由组数量，
并提供刷新及失败重试提示。
自定义组和 `auto/...` 策略的 ID 原样传给 Magpie，路由由 Magpie 执行。

可开启 **YOLO 自动执行**，免逐次工具确认。选择会跨新对话、文档和 Rhino 重启保留，
底部显示当前为 YOLO 或逐次确认；可随时在设置中切回逐次确认。
设计提问和假设尺度的确认保持独立。
停止后会等待已发出的 Rhino 操作返回，
再结束本轮，继续之前请检查模型。切换或关闭文档会停止当前回合。
每轮修改由 Rhino 撤销记录包围，可使用 `Ctrl+Z`。
刷新模型会保留当前选择，刷新期间也可点击停止。提问窗口支持单选、多选和补充说明。

## Requirements

- Windows x64, Rhino 9 with its built-in RhinoAI plugin enabled, and Magpie.
- A Magpie model or routing group with tool-call support.
- A local HTTP Magpie gateway. The connection file must contain a gateway root
  URL without `/v1`.

The native AI compatibility adapter retains RhinoAI's original WebView and adds
**Magpie** to its agent menu automatically. Magpie hosts this workspace inside
the AI panel; returning to native mode restores its original interface and agents.
The adapter changes the live UI through Eto controls and a bounded page extension.
It does not replace RhinoAI files or add a formal backend runner to its internal
registry. It only attaches to the known `Rhino.AI.UI.AIPanel` WebView shape and
keeps hosts per document. Rhino updates require a compatibility check.

If that UI shape is unavailable, the `Magpie` command falls back to the separate
panel. `MagpieStandalone` opens that panel explicitly. The modeling agent runs
directly against Magpie in either host.

The earlier v0.1 launcher remains under `src/RhinoMagpie` as a legacy component.
Use the current panel package for the direct integration described here.

## Configuration and document scope

The product display name is **Magpie-Rhino**, including its marketplace listing,
Magpie Agent entry, Rhino plugin title and release archive. Technical IDs
`rhino9-ai` and `rhino-magpie` remain stable for existing settings and Yak upgrades.
The named `Magpie-Rhino` folder avoids a generic `plugin` label for local installs.

The Magpie descriptor manages only `model`, `baseUrl` and `apiKey` in
`~/.config/rhino9-magpie/settings.json`. Disconnect in Magpie restores those keys.
The file contains a local gateway credential. Keep it private.

The panel reuses the MCP endpoint belonging to the active document. Listener
lookup uses a read-only reflection call to the installed RhinoAI host because
that version exposes no public per-document port API. When unavailable, the
panel starts a document listener using Rhino's `MCPStart` command.
That listener remains available until the document or Rhino session closes.
Other processes on the same computer can use it to run Rhino tools and scripts.

Tool results and conversations stay in memory until the panel closes. Prompts,
relevant model state and viewport images requested by the agent pass through
Magpie to the selected provider. Model compatibility and modeling quality vary.
Inspect created geometry and review any script or file-write approval.

Execution-mode preferences are stored separately in
`~/.config/rhino9-magpie/panel-preferences.json`. This file contains no gateway
credential, and disconnecting the Magpie connection does not reset the preference.
Fresh profiles default to tool confirmation; enabling YOLO remembers that choice.

## Build and checks

```powershell
node --test tests/agent.test.mjs
dotnet run --project tests/Core.Tests -c Release
dotnet run --project tests/Agent.Tests -c Release
dotnet run --project tests/Workflow.Tests -c Release
dotnet build src/RhinoMagpie.Plugin -c Release
./scripts/build-panel.ps1 -YakPath "E:/WORK/Rhino9/System/Yak.exe"
```

Streaming-detail browser regressions run with Playwright 1.60.0:
`node --test tests/browser/panel-scroll.test.mjs`. Set `PANEL_SCROLL_PLAYWRIGHT`
to an existing Playwright package directory, or install that pinned package and
its Chromium browser. CI runs these checks before packaging.

The Yak builder can also be downloaded from the official McNeel CLI download
page. CI uses the pinned Windows x64 Yak 0.19.1 builder and produces the `.yak`,
installer ZIP and SHA-256 checksums. GitHub tags publish release artifacts.

## Distribution

Magpie marketplace submission is tracked in `magpie-community/plugins#868`.
Rhino PackageManager/Yak and Food4Rhino are separate distribution channels.
Their actual submission state is recorded in `docs/validation.md`.

MIT. Unofficial community integration by mikidaren.

## Workflow references

The design draws on the user's architectural workflow requirements and Reer's
public product/tool documentation. `reer-ide/rhino_mcp` has an MIT license; its
remote-service repository declares proprietary terms. This workspace's UI and
agent control are our implementation, with no proprietary Reer code included.
