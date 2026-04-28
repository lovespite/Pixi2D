# tools/preview — 自举的 PXML 预览器

> v0.5 引入：用 PXML + JavaScript + Pixi2D.Host 自举重写 `Pixi2D.Preview` (WinForms) 的 MVP 范围功能。
> 旧 WinForms Preview 项目保留作为 fallback。

完整使用说明见 [`tools/preview/README.md`](../tools/preview/README.md)。

## 设计要点

* **零新控件**：所有 UI 全用既有 PXML 元素（`<panel>` / `<text-box multiline="true">` / `<switch>` / `<button>` / `<fancy-text>`）。
* **零新 `[JSExport]` 代理**：对象树/诊断列表通过 `Pxml.parse` + `UI.appendText` 桥渲染，绕过未代理的 TreeView/Table。
* **本身就是 demo**：作为最大体量的 PXML+JS 实战，验证 host pump、setInterval 在拖窗时的可靠性、fs 模块的可用性。

## 与旧 Preview (Pixi2D.Preview WinForms) 的差异

| 项目 | 旧版 (WinForms) | 新版 (PXML 自举) |
|---|---|---|
| 编辑器 | `RichTextBox` (含滚动条 / 撤销栈) | `<text-box multiline="true">`（< 500 行 .pxml 表现良好）|
| 对象树 | `TreeView` 控件 | 缩进 `FancyText` 列表（深度优先平铺）|
| 诊断 | `ListView` | 颜色分级 `FancyText` 列表 |
| AutoSave | `TextChanged` 事件 + 500ms timer | 200ms 文本轮询 + 500ms debounce |
| AutoHotReload | `FileSystemWatcher` | `fs.stat` 800ms 轮询（mtime 比较）|
| 文件对话框 | `OpenFileDialog` | `run.ps1 <path>` |
| 启动方式 | `dotnet run --project Pixi2D.Preview` | `tools\preview\run.ps1 [target.pxml]` |

## 已知缺口（→ v0.6 候选）

| 缺口 | 临时绕过 | 计划 |
|---|---|---|
| `TextBox` 没有 `TextChanged` 事件 | `setInterval(200ms)` 比较前后值 | 给 `TextBox` 加事件 + `TextBoxProxy.event TextChanged` |
| 编辑器无可见滚动条 / 鼠标滚轮 | 适合小文件 | v0.6 计划 `<scroll-view>` 控件 |
| 双击诊断不能跳转 | — | 需要 `TextBox.SelectionStart` 公共 setter |
| 没有 `OpenFileDialog` | 命令行参数 | P/Invoke 包装 Win32 OFN |
| TreeView/Table 没有 JS 代理 | `UI.appendText` 平铺 | 视使用频率补 `[JSExport]` 代理 |

## 涉及到的新基础设施

| 文件 | 职责 |
|---|---|
| `Pixi2D.Scripting/PxmlScriptApi.cs` | 注入 `globalThis.Pxml` / `globalThis.UI` |
| `Pixi2D.Host/PixiHostWindow.cs` | 调 `PxmlScriptApi.Install` + 注入 `globalThis.hostArgs` |
| `Pixi2D.Host/Program.cs` | CLI 收集 PXML 之后的位置参数到 `ExtraArgs` |
| `Controls/TextBox.cs` | `Multiline` 属性改为可写 setter (PXML 入口) |

详见 [`docs/scripting.md`](./scripting.md) §5。
