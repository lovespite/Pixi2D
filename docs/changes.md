# 改动清单 (feat/dsl-xml-js)

## v0.7 — AssetLoader (Phase A) ⏳

### Host 资源加载层 (`Pixi2D.Host/Assets/`)
新增 6 个文件：
- `IAssetProvider.cs` — provider 接口 + `IAssetWriteSink` 流式落盘抽象
- `AssetData.cs` — `AssetData(Bytes, ContentType, Source, FetchedAt, FromCache, DiskPath, SizeBytes, StatusCode, HeadersJson)` + `AssetProgress`
- `AssetCachePolicy.cs` — 默认 1 MiB 内存阈值 / 256 条 / 32 MiB / `%TEMP%/Pixi2D/AssetCache` / 512 MiB / 30s timeout
- `FileAssetProvider.cs` — `file://` + 相对路径，基于 PXML 目录解析
- `HttpAssetProvider.cs` — `HttpClient` 流式下载，双写内存 + sink；暴露 `RequestStart/RequestEnd/RequestError` (供 NetworkHook)
- `AssetCache.cs` — L1 LRU (LinkedList+Dict, 本地文件仅记入口) + L2 sidecar `.bin/.bin.meta` + EnforceDiskBudget
- `AssetLoader.cs` — 入口：URI 规范化 / dispatch / 并发去重 (`ConcurrentDictionary<string,Task<AssetData>>`) / Started/Loaded/Failed/Progress 事件

### PixiHostWindow 集成
- 字段 `_assets: AssetLoader`、`_uiQueue: ConcurrentQueue<Action>`
- 公开 `RunOnUiThread(Action)`：从 IO 线程把回调排到主 (脚本) 线程
- `PumpEngine()` 起始处 drain `_uiQueue`，再调 `_engine.Pump()`

### JS 代理 (`Pixi2D.Host/Scripting/AssetsProxy.cs`)
`globalThis.assets`：
- `loadText/loadBytes/loadJson(url) → int requestId`（异步，事件驱动）
- `loadTextSync/loadBytesSync/exists(url)`（仅 file://，HTTP → null）
- `clearCache() / clearMemoryCache() / clearDiskCache() / removeCache(url)`
- `cacheStats() → JSON 字符串`
- 事件：`loadedText / loadedBytes / loadedJson(int, string, string, string)` / `error(int, string, string)` / `progress(int, string, long, long)`
- bytes 跨 JS 边界以 base64 字符串传输

### Demo
`demos/12-assets/` — 加载本地 `sample.json` 填表 + Reload/Load Remote/Clear Cache/Stats 四个按钮。

### 文档
- `docs/assets.md`（新）—— 路径规范化 / 分级缓存 / 并发去重 / 错误诊断
- `docs/scripting.md` —— 追加「v0.7 新增：Assets 代理」节

### 待完成（Phase B / C）
- DebugBridge (TCP + JSON-line) + ConsoleHook / NetworkHook / FileTracker / TreeSerializer / EvalHandler
- `Pixi2D.Debugger` WinUI 3 项目 (元素树 / Console / Network / Files / REPL)

## v0.7 — DebugBridge (Phase B) ✅

### Host 调试桥 (`Pixi2D.Host/Debugging/`)
- `DebugBridge.cs` — `TcpListener` 单连接 + `Channel<string>` 写队列 + JSON-line 解析；`Send/SendWithId/OnRequest`
- `TreeSerializer.cs` — `Stage.Root` → JSON；`ConditionalWeakTable<DisplayObject,object>` 派发稳定 int id
- `DebugHost.cs` — 把 `Engine.OnLog` / `HttpAssetProvider.RequestStart/End/Error` / `AssetLoader.LocalFileTouched` 串到 bridge；接 `tree.refresh` / `eval` 入站；1 Hz tree.update 周期推送

### CLI / 启用
- `Program.cs` 新增 `--debug [port]`（默认 9229，仅 127.0.0.1）/ `--debug-wait`
- 未启用时所有 hook 不创建（zero-cost）

### 文档
- `docs/debugger.md`（新）—— 协议规范、帧表、消息样例

## v0.7 — Pixi2D.Debugger (Phase C) ✅

### 新项目 `Pixi2D.Debugger/`（独立 WinUI 3 unpackaged exe）
- `Pixi2D.Debugger.csproj` — `net10.0-windows10.0.19041.0` + `WindowsAppSDK 1.6.241114003`；显式 `Sdk.props/Sdk.targets` 导入；末尾 no-op 覆盖 `CopyLocalFilesOutputGroup` / `_GenerateProjectPriFileCore` / `_ComputeInputPris` / `GetMrtPackagingOutputs` / `_ValidateConfiguration` / `_GetProjectArchitecture` / `_GetDefaultResourceLanguage` / `GetOptionalProjectOutputs`，避开 VS-only `Microsoft.Build.AppxPackage.dll` / `Microsoft.Build.Packaging.Pri.Tasks.dll` 加载错误（unpackaged 不需要 PRI/Appx 任务）
- `app.manifest` — DPI Aware PerMonitorV2 + Win10/11 supportedOS GUIDs
- `App.xaml(.cs)` + `MainWindow.xaml(.cs)` — `Pivot` 5 个面板（Tree/Console/Network/Files/Eval）+ 顶部 host:port 连接条
- `Connection/DebugClient.cs` — TcpClient + JSON-line reader/writer + `EvalAsync` FIFO 等待 `evalResult`
- `Models/Models.cs` — `TreeNodeVm/ConsoleEntry/NetEntry/FileEntry`（带 `Display` 计算属性供 `ItemTemplate` 直接绑定）
- 主窗口订阅 `OnFrame` 事件，`DispatcherQueue.TryEnqueue` 切回 UI 线程后塞入 5 个 `ObservableCollection`；元素树以缩进文本扁平显示；Console / Files 自动去重 / 限长

### 加入解决方案
- `Pixi2D.sln` 加入 `Pixi2D.Debugger`
- 根 `Pixi2D.csproj` 增加 `Compile/EmbeddedResource/None Remove="Pixi2D.Debugger\**"`，避免 root project glob 抓到子项目源文件

### 验证
- `dotnet build Pixi2D.sln` 通过 (0 errors, 仅既有 4 warnings)
- `Pixi2D.Debugger.exe` 已生成于 `bin\Debug\net10.0-windows10.0.19041.0\`

### 用法
```
# 终端 1
Pixi2D.Host.exe demos\12-assets\main.pxml --debug
# 终端 2
Pixi2D.Debugger.exe   # 默认连 127.0.0.1:9229
```

---

## v0.6.1 — Table 增量更新 + TextBox 光标定位

### Table 增量更新 API (`Controls/Table.cs`)
内部新增 `_data` 可变副本（自 `DataSource` 拷贝）+ `_dirtyRows` HashSet + `_structuralDirty` 标志。
- `UpdateCell(int row, int col, string value)` — 修改单格；行高若变 → 后续行平移；不变 → 仅原地刷新已渲染 cell
- `UpdateRow(int row, params string[] cells)` — 覆盖整行（列数不等视为结构变更走全量）
- `AppendRow(params string[] cells)` / `InsertRow(int row, params string[] cells)` / `RemoveRow(int row)` — 结构变更
- `RecalculateLayout()` ≡ `NotifyDataChanged()` — 显式全量重测
- `RowCount` / `ColumnCount` 属性
- `Update()` tick 增加 `_dirtyRows` 增量分支：行高变化 → `_layoutDirty`；不变 → `RefreshDirtyRowCellsInPlace()` 仅修补 `_activeCells` 中匹配的 cell 文本+样式
- `DataSource` setter 现在拷贝外部引用到 `_data`（修改外部数组不再影响 Table；想生效需调 `NotifyDataChanged`）

### TextBox 光标定位 (`Controls/TextBox.cs`)
- `SetCursorPosition(int line1Based, int column1Based)` — 1-based 行列；列超过行末夹到 \n 前；line 超过末行夹到末尾
- 删除原同名空 stub

### JS 代理 (`Pixi2D.Scripting.QuickJs/Proxies.cs`)
- `TableProxy`：新增 `updateCell(row,col,value)` / `updateRow(row, cells)` / `appendRow(cells)` / `insertRow(row, cells)` / `removeRow(row)` / `recalculateLayout()`；带数组参数的方法走 `IJsonShimProxy`（`_shims` 新增 3 条）
- `TableStyleJson.ParseRow(json)` — 配套单行 JSON 解析
- `TableProxy.RowCount/ColumnCount` 改读 `Table.RowCount/ColumnCount`（之前读 `DataSource`）
- `TextBoxProxy.setCursorPosition(line, column)` — 替代原 `setCursor(x, y)`（同方法名/参数语义统一为 1-based 行列）
- `tools/preview/main.js`：`editor.setCursor` → `editor.setCursorPosition`

### 性能影响
高频局部更新（如逐秒滴答的状态行）从 O(全表 cell 重测+重建) 降到 O(脏行 cell 数)，且大多数情况下行高不变，仅原地修补文本，避免 cell pool 复用。

---

## v0.6 — Window 代理 + Table 样式 + Preview 自举完善

### Window 代理 (`globalThis.window`)

- `Pixi2D.Host/Scripting/WindowProxy.cs` — `[JSExport]` 暴露 `title` / `width` / `height` / `isFullScreen` / `pxmlPath` / `hostArgs` 属性 + `setTitle()` / `resize(w,h)` / `toggleFullScreen()` / `close()` / `requestRedraw()` 方法 + `resized(w,h)` / `closed()` / `fileChanged(path)` 事件
- `Pixi2D.Host/PixiHostWindow.cs` — 引擎构造后 `engine.SetGlobal("window", _windowProxy)`；watch 模式 FileSystemWatcher → `RaiseFileChanged`
- 已知警告 `QJSGEN003: HostArgs string[]`（SG 不支持 `string[]` 导出）：脚本侧仍用 `globalThis.hostArgs` fallback（PixiHostWindow.cs 字面量数组注入）

### Table 样式系统

- `Controls/TableStyle.cs`（新）— `TableStyle`（nullable 字段：`BackColor` / `Color` / `BorderColor` / `FontSize` / `HAlign`）+ `MergeWith` + `TableHAlign` 枚举
- `Controls/Table.cs`：
  - `HasHeader` / `DefaultStyle` / `HeaderStyle` + `Set{Table,Header,Row,Column,Cell}Style` / `ClearStyles`
  - `ResolveStyle(r,c)` 合并优先级：Default → Header(row=0&&hasHeader) → Column → Row → Cell
  - `UpdateVisibleCells` 强制 `cell.ApplyStyle(ResolveStyle(r,c))`，避免 cell pool 复用样式残留
  - `event Action<int,int,string>? CellClicked` + `event Action<int>? RowClicked`（cell 内部 mouse-down 路由到 `OnCellMouseDown`）
- `TableCell` 加 `FontSize` / `HAlign` 字段 + `ApplyStyle` / `ResetStyleToDefaults`

### Table JS 代理

- `Pixi2D.Scripting.QuickJs/Proxies.cs::TableProxy` — `setData(rows)` / `clear()` / `setTableStyle(s)` / `setHeaderStyle(s)` / `setRowStyle(r,s)` / `setColumnStyle(c,s)` / `setCellStyle(r,c,s)` / `clearStyles()` + `cellClicked` / `rowClicked` 事件
- 由于 SG 不支持 `string[][]` / `float?` / 自定义 POCO 自动 unwrap，C# 端方法签名全为 `string json`；脚本端通过 `IJsonShimProxy` 自动安装 monkey-patch wrapper（`Pixi2D.Scripting/IControlProxy.cs::IJsonShimProxy` + `ScriptBootstrap.EmitJsonShim`），用户调用 `setData([[...]])` 等同 `setData(JSON.stringify(...))`
- `QuickJsProxyFactory.cs` — `Table → TableProxy`（在 `Container` case 之前匹配）
- 颜色字符串解析：`#RGB` / `#RRGGBB` / `#RRGGBBAA`；align：`left` / `center` / `right`
- JSON 解析使用 `System.Text.Json.JsonDocument`（AOT 兼容；无反射）

### Preview 自举完善

- `tools/preview/main.pxml`：诊断面板从 `<panel id="diagPanel">` 改为 `<table id="diagTable">`
- `tools/preview/main.js`：
  - `renderDiagnostics` 用 `diagTable.setData([['Severity','Line','Col','Element','Message'], ...rows])`
  - 错误行 `setRowStyle(i+1, {backColor:'#3a1f24', color:'#ff6b6b'})`；警告行 `{color:'#f1c40f'}`
  - `diagTable.on('cellClicked', (row,_c,_t) => editor.scrollToLine(_lastDiags[row-1].line))` — 点击诊断行直接跳到编辑器对应行（hasHeader=true 时数据行从 1 起）

---

## v0.4 — Host Pump + 可运行 Demo 库

### Host Pump（让 setTimeout / setInterval 真正滴答）

- `external/qjs.net` @ `35819f94` → `ad6c748`：
  - `35819f94` — 新增 `public QuickJSEngine.PumpEventLoop()` / `QuickJSRuntime.PumpEventLoop()`（非阻塞单次 `EventLoop.DrainQueue`）
  - `ad6c748` — `EventLoop.ProcessTimers` 改用 cumulative scheduling（`next = previous_deadline + interval`），消除 setInterval 漂移；超过一个完整 interval 落后时 snap-forward 防爆炸
- `Pixi2D.Markup/IScriptEngine.cs` — 接口默认方法 `void Pump() {}`；`NullScriptEngine` 沿用空
- `Pixi2D.Scripting.QuickJs/QuickJsScriptEngine.cs` — `Pump()` 转发到底层引擎
- `Pixi2D.Host/HostNative.cs` — `LibraryImport` P/Invoke `SetTimer` / `KillTimer`（**零 WinForms 依赖**）
- `Pixi2D.Host/PixiHostWindow.cs` — `OnLoad` 注册 16ms `WM_TIMER`；override `HandleWndProc` 拦截 `WM_TIMER` → `_engine.Pump()`；`Dispose` 调 `KillTimer`
- `Pixi2D.Host.csproj` — `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`（`LibraryImport` source-gen 需求）
- 影响：`setTimeout` / `setInterval` / `clearTimeout` / `clearInterval` / `queueMicrotask` 现在在 Host 渲染期间真正滴答，**且在窗口拖动 / 缩放 / 模态消息循环期间也不会被冻结**（`WM_TIMER` 在 size/move modal loop 里仍被派发）
- 退化：若 `SetTimer` 失败，回退到 `OnPaint` 心跳；最小有效间隔 ~16ms

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

## v0.5 (2026-04)

### 自举 PXML Preview (`tools/preview`)

把 `Pixi2D.Preview` (414 行 WinForms) 用 PXML+JS 重写一份作为最大体量 demo，验证 DSL/事件循环/IO 在真实工具下的可用性。
旧 `Pixi2D.Preview` 项目保留作为 fallback。详见 [`docs/preview.md`](./preview.md)。

### 新基础设施

* `Pixi2D.Scripting.PxmlScriptApi.Install(engine, host)`：JS 端获得 `globalThis.Pxml` (parse) + `globalThis.UI` (clear/appendText/setText/getText/exists)。
  全部走基础类型 + 手写 JSON, AOT-friendly。
* `Pixi2D.Host` CLI：PXML 之后的位置参数收集到 `globalThis.hostArgs: string[]`。
* `Controls/TextBox.cs`: `Multiline` 由只读改为可写属性（PXML 入口；运行时切换 `WordWrap` / `MaxWidth`）。

### Breaking changes

无。`PixiHostWindow` ctor 新增 `string[]? extraArgs = null` 可选参数；`CliOptions` 新增 `ExtraArgs`。
