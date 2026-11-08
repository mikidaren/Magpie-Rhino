# Food4Rhino listing draft

Submission remains pending Rhino Account access.

## Listing fields

| Field | Value |
| --- | --- |
| Product | RhinoMagpie |
| Version | 0.2.0 |
| Maintainer | mikidaren |
| License | MIT |
| Price | Free |
| Platform | Windows x64 |
| Rhino | Rhino 9 |
| Package | rhino-magpie |
| Website | https://github.com/mikidaren/rhino9-magpie |
| Downloads | https://github.com/mikidaren/rhino9-magpie/releases/tag/v0.2.0 |
| Support | https://github.com/mikidaren/rhino9-magpie/issues |

## Description

RhinoMagpie adds a native dockable Magpie AI modeling panel to Rhino 9.
Choose your model, custom model group or routing strategy in the Magpie desktop
application. The panel sends that selection directly to the local Magpie gateway
and calls Rhino's built-in MCP tools to inspect and edit native geometry.

The panel includes per-document conversations, a live model/group catalog,
tool confirmation, cancellation and viewport image feedback. Its `Magpie`
command works from an ordinary Rhino launch.

Install the Rhino `.yak` package and the companion Magpie connection plugin.
Rhino 9's built-in RhinoAI plugin must be enabled. A tool-capable model and the
local Magpie gateway are required.

This is an unofficial community plugin. Initial verification covers a Windows
Rhino 9 installation, native solid geometry creation with a custom Magpie group,
geometry readback and undo. Users should inspect generated geometry before use.

## 中文介绍

RhinoMagpie 在 Rhino 9 内提供可停靠的 Magpie AI 建模面板。
它直接使用 Magpie 中选择的模型、自定义组和策略，通过 Rhino MCP 检查、
创建和修改原生几何。支持按文档保留对话、模型列表、修改确认、停止和视口反馈。

安装配套 Rhino `.yak` 包和 Magpie 连接插件后，在 Rhino 输入 `Magpie` 即可使用。
初始测试已验证自定义组调用、实体创建、几何读回和撤销。
