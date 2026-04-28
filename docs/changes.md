# 改动清单 (feat/dsl-xml-js)

## v0.2 — 错误诊断 + 内联编辑器 + 更多示例

### 新增

- `Pixi2D.Markup/Diagnostics/Diagnostic.cs` — `Diagnostic` record + `DiagnosticSeverity { Info, Warning, Error }`
- `Pixi2D.Markup/Diagnostics/PxmlException.cs` — 抽象 `PxmlException` 基类 + `PxmlParseException` / `PxmlUnknownElementException` / `PxmlAttributeException` / `PxmlStructureException`
  - 携带 `FilePath / Line / Column / ElementName / AttributeName`
  - `ToString()` 编译器风格：`path(line,col): TypeName: <tag> @attr message`
  - `ToDiagnostic()` 转换为 `Diagnostic`（severity=Error）
- `Pixi2D.Markup/Examples/form-login.pxml`
- `Pixi2D.Markup/Examples/dashboard.pxml`
- `Pixi2D.Markup/Examples/tree-explorer.pxml`
- `Pixi2D.Markup/Examples/table-data.pxml`
- `Pixi2D.Markup/Examples/modal-confirm.pxml`

### 修改

- `Pixi2D.Markup/PxmlLoader.cs`
  - 全部抛点替换为 `PxmlException` 子类，从 `IXmlLineInfo` 取行列号
  - 记录当前 `_currentFile`，新增 `LoadFromString(xml, virtualPath)`
  - 未知/不可写属性、被忽略的内文 → 收集到 `Diagnostics` 列表（warning），不再静默丢弃
  - 新增公共 `Diagnostics` 属性
- `Pixi2D.Markup/ElementRegistry.cs`
  - 新增 `HasFactory(name)` 方便诊断区分类型 vs 工厂
- `Pixi2D.Preview/MainForm.cs` —— 大改 (v0.2 预览器)
  - 主 `SplitContainer` 左编辑器 + 右上对象树 + 右下 Diagnostics ListView
  - Multiline TextBox + Consolas 等宽字体内联编辑
  - 编辑文本 500ms 防抖 → AutoSave (写文件) + AutoHotReload (重新解析)
  - `FileSystemWatcher`：自身保存时 800ms 屏蔽以避免循环；外部修改在编辑器无未保存改动时反向同步
  - Diagnostics ListView 列：Severity / Line / Col / Element / Attribute / Message；双击行跳转编辑器对应位置
  - 状态栏：`AutoSave: ON  AutoReload: ON   Errors: N  Warnings: M`
  - 选项菜单可单独切换 AutoSave / AutoHotReload；`Ctrl+S` 立即保存

---

## v0.1 — 初始批次

### 新增

#### 项目
- `Pixi2D.Markup/` — DSL 类库 (net9.0)
  - `ElementRegistry.cs` — 元素名 ↔ 控件类型映射
  - `ValueConverters.cs` — 字符串 → .NET 类型转换
  - `IScriptEngine.cs` — 脚本引擎抽象 + `ScriptHost` + `NullScriptEngine`
  - `PxmlLoader.cs` — PXML 反射加载器
  - `Schema/pixi2d.xsd` — XML schema (v0.1)
  - `Examples/*.pxml` — 示例文件 (hello / layout / nested)
- `Pixi2D.Preview/` — WinForms 预览器 (net9.0-windows)
  - `MainForm.cs` — 拖拽 / 打开 / 热重载 / 树形视图

#### Pixi2D 主项目
- `Core/UIContext.cs` — 全局静态上下文 (DWriteFactory / DefaultTextFactory / Assets / 默认字体) + Push/Pop 作用域
- `Core/IAssetLoader.cs` — 资源加载抽象 + `NullAssetLoader`
- `Core/Text.cs::CreateDefault(string)` — 便捷工厂

### 修改

#### 控件无参构造（保留原签名，链式调用）
- `Core/Text.cs`
- `Components/Panel.cs`
- `Components/Modal.cs` (private ctor → public)
- `Components/Utils/SoftKeyboard.cs`
- `Controls/Button.cs`
- `Controls/FancyText.cs`
- `Controls/Number.cs`
- `Controls/Switch.cs`
- `Controls/ComboBox.cs` (含 `ComboBoxItem`)
- `Controls/TextBox.cs`
- `Controls/Table.cs` (含 `TableCell`)
- `Controls/TreeView.cs` (含 `TreeNode`)
- `Controls/ListItem.cs`
- `Controls/ProgressBar.cs`
- `Controls/ScrollableList.cs`
- `Controls/VirtualScrollList.cs`
- `Controls/GraphicsSpinLoading.cs`

### 不做的事 (本批次范围外)

- IconLabel / FancyButton / SpinLoading / MessageBox 的无参构造 — 改用 `ElementRegistry.RegisterFactory`，需结合 `IAssetLoader`
- VirtualScrollList<T> 元素注册 — 泛型，需用户为具体 T 注册
- 实际 JS 引擎实现 — 仅有 `IScriptEngine` / `NullScriptEngine`
- 预览器中真正的 Direct2D 渲染视图 — 当前只展示对象树

### 兼容性

- 所有原有公共构造完全保留
- 主项目无新增 NuGet 依赖
- WinForms 仅在 `Pixi2D.Preview` 引入，主项目保持纯净

### 项目
- `Pixi2D.Markup/` — DSL 类库 (net9.0)
  - `ElementRegistry.cs` — 元素名 ↔ 控件类型映射
  - `ValueConverters.cs` — 字符串 → .NET 类型转换
  - `IScriptEngine.cs` — 脚本引擎抽象 + `ScriptHost` + `NullScriptEngine`
  - `PxmlLoader.cs` — PXML 反射加载器
  - `Schema/pixi2d.xsd` — XML schema (v0.1)
  - `Examples/*.pxml` — 示例文件 (hello / layout / nested)
- `Pixi2D.Preview/` — WinForms 预览器 (net9.0-windows)
  - `MainForm.cs` — 拖拽 / 打开 / 热重载 / 树形视图

### Pixi2D 主项目
- `Core/UIContext.cs` — 全局静态上下文 (DWriteFactory / DefaultTextFactory / Assets / 默认字体) + Push/Pop 作用域
- `Core/IAssetLoader.cs` — 资源加载抽象 + `NullAssetLoader`
- `Core/Text.cs::CreateDefault(string)` — 便捷工厂

## 修改

### 控件无参构造（保留原签名，链式调用）
- `Core/Text.cs`
- `Components/Panel.cs`
- `Components/Modal.cs` (private ctor → public)
- `Components/Utils/SoftKeyboard.cs`
- `Controls/Button.cs`
- `Controls/FancyText.cs`
- `Controls/Number.cs`
- `Controls/Switch.cs`
- `Controls/ComboBox.cs` (含 `ComboBoxItem`)
- `Controls/TextBox.cs`
- `Controls/Table.cs` (含 `TableCell`)
- `Controls/TreeView.cs` (含 `TreeNode`)
- `Controls/ListItem.cs`
- `Controls/ProgressBar.cs`
- `Controls/ScrollableList.cs`
- `Controls/VirtualScrollList.cs`
- `Controls/GraphicsSpinLoading.cs`

### Pixi2D.csproj
- 新增 `<Compile Remove="Pixi2D.Markup\**" />`、`<Compile Remove="Pixi2D.Preview\**" />` (含 EmbeddedResource/None)，避免主项目把子项目文件也编译进去。

### Pixi2D.sln
- 新增 `Pixi2D.Markup` 与 `Pixi2D.Preview`。

## 不做的事 (本批次范围外)

- IconLabel / FancyButton / SpinLoading / MessageBox 的无参构造 — 改用 `ElementRegistry.RegisterFactory`，需结合 `IAssetLoader`。
- VirtualScrollList<T> 元素注册 — 泛型，需用户为具体 T 注册。
- 实际 JS 引擎实现 — 仅有 `IScriptEngine` / `NullScriptEngine`。
- 预览器中真正的 Direct2D 渲染视图 — v0.1 只展示对象树。

## 兼容性

- 所有原有公共构造完全保留。
- 没有任何方法/属性被标记为 `[Obsolete]` (除已存在的 `List`/`ScrollableList`)。
- 主项目无新增 NuGet 依赖。
- WinForms 仅在 `Pixi2D.Preview` 引入，主项目保持纯净。
