# 改动清单 (feat/dsl-xml-js)

## v0.4 — Host Pump + 可运行 Demo 库

### Host Pump（让 setTimeout / setInterval 真正滴答）

- `external/qjs.net` @ `35819f94` — 新增 `public QuickJSEngine.PumpEventLoop()` / `QuickJSRuntime.PumpEventLoop()`（非阻塞单次 `EventLoop.DrainQueue`），upstream-friendly 改动
- `Pixi2D.Markup/IScriptEngine.cs` — 接口默认方法 `void Pump() {}`；`NullScriptEngine` 沿用空
- `Pixi2D.Scripting.QuickJs/QuickJsScriptEngine.cs` — `Pump()` 转发到底层引擎
- `Pixi2D.Host/PixiHostWindow.cs` — `OnPaint` 在 `_stage.Render` 之前调用 `_engine?.Pump()`，异常吞掉转日志
- 影响：`setTimeout` / `setInterval` / `clearTimeout` / `clearInterval` / `queueMicrotask` 现在在 Host 渲染期间真正滴答；最小有效间隔 ≈ 渲染 fps（~16ms）

### `demos/` 目录（仓库根，新）

- `demos/run.ps1` — `-List` / `-Name <模糊>` / `-NoBuild` / `-NoWatch` / `-Configuration`
- `demos/README.md` — demo 索引 + 启动方式 + 编写指南
- 12 个 demo（01-hello / 02-counter / 03-login / 04-calculator / 05-form-validation / 06-theme-toggle / 07-modal-flow / 08-progress-stepper / 09-switch-grid / 10-dashboard / 11-stopwatch / 12-progress-animate）
- 每个目录含 `main.pxml`、可选 `main.js`、`README.md`（首行非标题被 `run.ps1 -List` 抓为描述）

### 文档

- `docs/demos.md` — demo 索引（与 demos/README 对应）
- `docs/host.md` — 补 Pump 段
- `README.md` — 加 demos 链接

### 关键修复 / Tips

- TextBox 的 placeholder 属性名实际是 `PlaceholderText`；PXML 应写 `placeholder-text="..."`，不是 `placeholder=`
- `setInterval` 在 Host 中不会自动滴答 —— 必须由 `_engine.Pump()` 推动（v0.4 已内置）

---

## v0.3 — QuickJS 集成 + D2DWindow 渲染宿主

### 子模块

- `external/qjs.net` ← https://github.com/lovespite/qjs.net.git (MIT)
- `external/D2DWindow` ← https://github.com/lovespite/D2DWindow.git
- 克隆仓库后必须 `git submodule update --init --recursive`。

### 新增项目

- `Pixi2D.Scripting/Pixi2D.Scripting.csproj` (`net9.0`, `IsAotCompatible=true`)
  - `IControlProxy` / `IProxyFactory`：脚本桥接抽象，零反射。
  - `ScriptBootstrap.Install / BindNamedObjects / ApplyOnAttributes`：脚本环境装配流程。
  - `ConsoleShim`：注册 `console.log/info/warn/error`。
- `Pixi2D.Scripting.QuickJs/Pixi2D.Scripting.QuickJs.csproj` (`net10.0-windows`, `IsAotCompatible=true`)
  - `QuickJsScriptEngine : IScriptEngine`：包装 `QuickJsNet.QuickJSEngine`。
  - `Proxies.cs`：10 个 `[JSExport] partial XxxProxy`（DisplayObject/Container/Panel/Button/FancyText/TextBox/Switch/Number/ProgressBar/Modal）。
  - `QuickJsProxyFactory`：`switch (control)` 静态分派。
  - 引用 `QuickJsNet.SourceGenerators` 作 Analyzer，编译期生成绑定。
- `Pixi2D.Host/Pixi2D.Host.csproj` (`net10.0-windows`, `Exe`, `RuntimeIdentifier=win-x64`)
  - `Program.Main`：CLI + AllocConsole/AttachConsole + 启动循环。
  - `PixiHostWindow : Direct2D1Window`：渲染 Stage、转发输入事件、管理 QuickJS 生命周期、`--watch` 热重载。
  - 显式 `<Content Include="..\external\qjs.net\qjs.net\quickjs.dll" CopyToOutputDirectory="PreserveNewest" />`。

### 修改

- `Pixi2D.Markup/IScriptEngine.cs`
  - 新增 `RegisterFunction(string, Func<object?[], object?>)` 抽象方法
  - `NullScriptEngine` 提供空实现
- `Pixi2D.csproj` —— 编译排除 `external/**`、新增 `Compile Remove` 规则
- `Pixi2D.sln` —— 加入 4 个新项目（含子模块）

### 示例

- `Pixi2D.Markup/Examples/scripted-counter.pxml` + `.js` —— Switch 切换计数器
- `Pixi2D.Markup/Examples/scripted-login.pxml` + `.js` —— TextBox 登录校验

### 文档

- `docs/scripting.md` —— [JSExport] 代理列表、`obj.on('event', fn)` 订阅模式、AOT 边界
- `docs/host.md` —— CLI 参数、AllocConsole 策略、启动时序、watch 模式

### 关键技术决策

- **零运行时反射**：所有 .NET ↔ JS 桥接走 `qjs.net` 的 `[JSExport] partial class` source generator；`QuickJsProxyFactory` 用静态 `switch` 取代类型反射。
- **事件订阅**：JS 端统一 `obj.on('camelCase', fn)` / `off`（与 qjs.net 内置语义一致），不使用 `obj.onClick = fn`。
- **on-* 胶水**：在用户脚本之后 emit 一行 JS `if (typeof handler === 'function') id.on('event', handler);`，仍然零反射。
- **`OnDeviceReady` 时序坑**：`Direct2D1Window` 在基类 ctor 期间同步触发 `OnDeviceReady`，此时派生字段为空；`BuildScene` 必须放在 `OnLoad`（`Run()` 之后）。

---

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
