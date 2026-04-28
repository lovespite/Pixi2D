# Pixi2D

基于 SharpDX / Direct2D 的 .NET 9 即时模式 UI 库。

## 项目结构

| 项目 | 说明 |
|---|---|
| `Pixi2D` | 核心库 (net9.0)，所有 `DisplayObject` / 控件 / 组件 |
| `Pixi2D.Markup` | DSL 库 (net9.0)，PXML XML 加载器 + 脚本引擎抽象 |
| `Pixi2D.Preview` | WinForms 预览器 (net9.0-windows)，加载 `.pxml` 并显示对象树 |

## DSL & Scripting

`feat/dsl-xml-js` 分支引入 XML(DSL) + JavaScript 支持的前期准备。详见：

- [`docs/dsl-overview.md`](docs/dsl-overview.md) — 设计哲学与初始化样板
- [`docs/element-mapping.md`](docs/element-mapping.md) — PXML 元素 / 属性映射表
- [`docs/changes.md`](docs/changes.md) — 本批次改动清单
- [`Pixi2D.Markup/Schema/pixi2d.xsd`](Pixi2D.Markup/Schema/pixi2d.xsd) — XML schema (v0.1)
- [`Pixi2D.Markup/Examples/`](Pixi2D.Markup/Examples/) — 示例 .pxml 文件

快速体验：

```powershell
dotnet build Pixi2D.sln
dotnet run --project Pixi2D.Preview -- Pixi2D.Markup\Examples\hello.pxml
```
