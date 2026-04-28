# Pixi2D Preview (self-hosted)

用 PXML + JavaScript + Pixi2D.Host 自举的 .pxml 预览/编辑器，
等价于旧 `Pixi2D.Preview` (WinForms) 的 MVP 范围功能。

## 启动

```powershell
# 不带目标 → 空白编辑器（可粘贴 .pxml 并实时预览解析诊断）
.\tools\preview\run.ps1

# 加载某个 .pxml；编辑/磁盘双向同步
.\tools\preview\run.ps1 demos\02-counter\main.pxml
```

`run.ps1` 内部会自动构建 `Pixi2D.Host`（如果尚未存在），然后用：

```
Pixi2D.Host.exe tools\preview\main.pxml --watch <target.pxml>
```

`<target.pxml>` 通过命令行额外位置参数传入，由 Host 包装为 `globalThis.hostArgs[0]`，main.js 解析使用。

## 功能

| 区域       | 说明 |
|------------|------|
| 顶栏       | 当前路径、Reload、AutoSave 开关、AutoReload 开关 |
| 左侧编辑器 | 多行 `<text-box multiline="true">`，支持复制 / 粘贴 |
| 右上对象树 | `Pxml.parse` 出的元素层级（缩进显示） |
| 右下诊断   | `Diagnostic` 列表（颜色按 severity 分级） |
| 状态栏     | 解析结果摘要 / 自动保存 / 自动重载状态 |

## 已知限制（v0.5）

- 编辑器无滚动条/语法高亮 —— 适合 < ~500 行 .pxml
- 暂无文件对话框；新建/打开通过 `run.ps1 <path>` 切换
- 双击诊断暂不跳转编辑器位置（缺 `TextBox.SelectionStart` 公共 setter）
- 旧 `Pixi2D.Preview` (WinForms) 项目保留作为 fallback：
  ```
  dotnet run --project Pixi2D.Preview
  ```

## 涉及到的新 JS API

详细定义见 `docs/scripting.md`：

- `Pxml.parse(text, virtualPath?) → { ok, diagnostics:[…], tree:[…] }`
- `UI.clear(id)` / `UI.appendText(id, text, color?, fontSize?)`
- `UI.setText(id, text)` / `UI.getText(id)` / `UI.exists(id)`
- `globalThis.hostArgs: string[]`：Host 命令行 PXML 之后的额外位置参数
- `fs` / `fsAsync`：来自 qjs.net，默认安装

## 设计动机

用宿主自身的 DSL 把工具重写一遍，既验证 DSL/事件循环/IO 的实际可用性，也暴露所有缺口（例如 TextBox 缺事件、TreeView 缺 JS proxy → 我们走 `UI.appendText` 桥接绕过）。新的缺口会在 v0.6 修补。
