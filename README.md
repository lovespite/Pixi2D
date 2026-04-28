# Pixi2D

基于 SharpDX / Direct2D 的 .NET 9 即时模式 UI 库。

## 项目结构

| 项目 | 说明 |
|---|---|
| `Pixi2D` | 核心库 (net9.0)，所有 `DisplayObject` / 控件 / 组件 |
| `Pixi2D.Markup` | DSL 库 (net9.0)，PXML XML 加载器 + 脚本引擎抽象 |
| `Pixi2D.Preview` | WinForms 预览器 (net9.0-windows)，加载 `.pxml` 并显示对象树 |
| `Pixi2D.Scripting` | 脚本宿主抽象 (net9.0, AOT 兼容)，`IControlProxy` / `ScriptBootstrap` / `ConsoleShim` |
| `Pixi2D.Scripting.QuickJs` | QuickJS 适配器 (net10.0-windows, AOT 兼容)，`[JSExport] partial` 控件代理 |
| `Pixi2D.Host` | PXML+JS 渲染宿主 (net10.0-windows, exe)，基于 `D2DWindow`，含嵌入控制台 |

## DSL & Scripting

`feat/dsl-xml-js` 分支引入 XML(DSL) + JavaScript 支持。详见：

- [`docs/dsl-overview.md`](docs/dsl-overview.md) — 设计哲学与初始化样板
- [`docs/element-mapping.md`](docs/element-mapping.md) — PXML 元素 / 属性映射表
- [`docs/scripting.md`](docs/scripting.md) — JS 端 API、`[JSExport]` 代理、AOT 边界
- [`docs/host.md`](docs/host.md) — `Pixi2D.Host` CLI / 控制台 / Pump / watch
- [`docs/demos.md`](docs/demos.md) — **可运行 Demo 索引（12 个 PXML+JS 演示）**
- [`docs/changes.md`](docs/changes.md) — 改动清单 (v0.1 / v0.2 / v0.3 / v0.4)
- [`Pixi2D.Markup/Schema/pixi2d.xsd`](Pixi2D.Markup/Schema/pixi2d.xsd) — XML schema (v0.1)
- [`Pixi2D.Markup/Examples/`](Pixi2D.Markup/Examples/) — 内置最小示例 .pxml
- [`demos/`](demos/) — **可运行 demo 套件**

快速体验：

```powershell
git submodule update --init --recursive
dotnet build Pixi2D.sln

# 预览器 (WinForms 工具链)
dotnet run --project Pixi2D.Preview -- Pixi2D.Markup\Examples\hello.pxml

# Host: 渲染 + 跑 JS 脚本 (同名 .js 自动加载)
dotnet run --project Pixi2D.Host -- Pixi2D.Markup\Examples\scripted-counter.pxml --watch

# 一键体验所有 demo
.\demos\run.ps1 -List
.\demos\run.ps1 -Name stopwatch
```
