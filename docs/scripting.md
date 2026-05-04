# Pixi2D Scripting (QuickJS)

> 适用版本：v0.6。本文档描述 PXML + JavaScript 集成的设计与使用。

## 0. JS 命名规则（必读）

> **所有 .NET 端 `PascalCase` 标识符在 JS 端一律 `camelCase`**（属性、方法、事件名）。
> 这是 `qjs.net` 的 `[JSExport]` source generator 自动转换（参见
> `external/qjs.net/QuickJsNet.SourceGenerators/JSExportGenerator.cs:308 ResolveJsName → ToCamelCase`）。

```js
// ✅ 正确
editor.text = '...';            // C# TextBox.Text
swAutoSave.isOn = true;         // C# Switch.IsOn
lblPath.content = '...';        // C# FancyText.Content
btn.on('click', fn);            // C# Button.Click 事件

// ❌ 错误（运行时静默失败 → undefined 写入 / 读到 undefined）
editor.Text = '...';
swAutoSave.IsOn = true;
lblPath.Content = '...';
btn.on('Click', fn);
```

如需自定义 JS 名（覆盖默认 camelCase）：在 .NET 端用 `[JSName("foo")]` 或 `[JSExport("Foo")]`（类名）。

## 1. 总体架构

```
┌──────────────────┐    ┌──────────────────────────┐
│  PxmlLoader      │───▶│  ScriptHost              │
│  (Pixi2D.Markup) │    │   .NamedObjects (id→ctl) │
└──────────────────┘    │   .PendingHandlers       │
                        └────────────┬─────────────┘
                                     │
        ┌────────────────────────────▼───────────────────────────────┐
        │  ScriptBootstrap.Install (Pixi2D.Scripting)                │
        │   1. ConsoleShim.Install   → 注册 console.log/warn/...     │
        │   2. NamedObjects → IProxyFactory.Create → engine.SetGlobal│
        │  --- 此处用户脚本 engine.Execute(jsSrc) ---                 │
        │   3. ScriptBootstrap.ApplyOnAttributes                     │
        │      → emit `id.on('event', handler)` 胶水                  │
        └─────────────────────────────────────────────────────────────┘
                                     │
                ┌────────────────────▼─────────────────────┐
                │  QuickJsScriptEngine + [JSExport] proxies│
                │  (Pixi2D.Scripting.QuickJs, AOT-ready)   │
                └──────────────────────────────────────────┘
```

* **零运行时反射**：所有 .NET ↔ JS 桥接走 `qjs.net` 的 `[JSExport] partial class` source generator。
* **静态分派**：`QuickJsProxyFactory.Create` 用 `switch (control)` 决定代理类型，无类型反射。
* **on-* 转 JS**：PXML 的 `on-click="onLogin"` 在脚本执行后通过字符串拼接 `btnLogin.on('click', onLogin);` 完成绑定，依然零反射。

## 2. JS 端 API

### 2.1 控件代理（id 命名）

PXML 中带 `id="xxx"` 的控件会以同名全局变量注入 JS。

| 控件类型 | JS 属性 | JS 事件 (`obj.on('name', fn)`) |
|---|---|---|
| `Button`      | `text`, `id`, `x`,`y`,`width`,`height`,`visible` | `click` |
| `Switch`      | `isOn` / `checked`, ...                          | `changed(bool)` |
| `FancyText`   | `text` / `content`                               | — |
| `TextBox`     | `text` / `value`, `placeholder`, `readOnly`      | — |
| `Number`      | `value` (double), `format`, `prefix`, `suffix`   | — |
| `ProgressBar` | `value` (double)                                 | — |
| `Modal`       | `visible`, `show()`, `hide()`                    | — |
| 其它 (Container/Panel) | 通用 DisplayObject 属性                  | — |

> 命名规则：C# `PascalCase` → JS `camelCase`（由 `qjs.net` 源代码生成器统一处理）。
>
> 事件订阅统一使用 `obj.on('camelCaseName', fn)` 与 `obj.off('camelCaseName', fn)`。

### 2.2 console

`console.log / info / warn / error` 自动可用，由 `ConsoleShim` 通过 `engine.RegisterFunction` + 一段 JS 胶水注册：

```js
console.log("hi", 1, true);  // → Console.Out 或宿主提供的 log 回调
```

### 2.3 PXML on-* 等价 JS

```xml
<button id="btnInc" on-click="onInc" />
```

等价于在用户脚本之后执行：

```js
if (typeof onInc === 'function') btnInc.on('click', onInc);
```

> 事件名转换：PXML kebab `on-click` → 注册名 `Click` (PxmlLoader) → JS 事件 `click` (camelCase)。

## 3. 添加新控件代理

1. 在 `Pixi2D.Scripting.QuickJs/Proxies.cs` 增加：

   ```csharp
   [JSExport]
   public partial class MyControlProxy : IControlProxy
   {
       private readonly MyControl _c;
       public event Action? SomeEvent;
       public MyControlProxy(MyControl c) { _c = c; _c.OnSomething += () => SomeEvent?.Invoke(); }
       DisplayObject IControlProxy.Wrapped => _c;
       public string Id { get => _c.Name ?? ""; set => _c.Name = value; }
       // 暴露 PascalCase 属性即可
   }
   ```

2. 在 `QuickJsProxyFactory.Create` 的 `switch` 中追加 `MyControl c => new MyControlProxy(c),`。

3. 注意：
   * 类必须 `partial`、不能继承非 `partial` 基类（采用组合而非继承）。
   * 事件签名只能用基础类型（`Action`、`Action<bool>`、`Action<string>` 等），避免跨边界 marshalling 不确定。
   * 复杂返回值用 `string` JSON 而非自定义 class。

## 4. AOT 边界

* 本仓库新增的 `Pixi2D.Scripting` 与 `Pixi2D.Scripting.QuickJs` 项目均启用 `<IsAotCompatible>true</IsAotCompatible>`，运行时**零反射**。
* `Pixi2D.Markup.PxmlLoader` 当前用反射写入控件属性，**尚未** AOT-clean（v0.4 议题）。
* SharpDX / Direct2D 互操作链亦未承诺 AOT；本批次只承诺新代码。

## 5. v0.5 新增：Preview / 工具脚本辅助 API

下面这组 API 由 `Pixi2D.Scripting.PxmlScriptApi.Install` 注册，专为**自举工具**（如 `tools/preview`）准备，避免给每个新控件都补 `[JSExport]` 代理。所有数据用基础类型 + JSON 跨边界。

### 5.1 `Pxml.parse(text, virtualPath?) → result`

把任意 PXML 文本解析为对象树并收集诊断；不抛异常。

```js
const r = Pxml.parse(text, '/path/to/file.pxml');
// r = {
//   ok: true | false,
//   diagnostics: [{ severity: 'Error'|'Warning'|'Info',
//                   line, column, element?, attribute?, message, file? }, ...],
//   tree: [{ depth, type, id }, ...]   // 深度优先平铺
// }
```

### 5.2 `UI.*` 容器操作（按 PXML id 寻址）

```js
UI.clear(id);                              // 清空 Container/Panel 内容（Panel 保留背景）
UI.appendText(id, text, color?, fontSize?);// 追加 FancyText, 颜色 #RGB / #RRGGBB
UI.setText(id, text);                      // 写 Button/TextBox/FancyText/Text 等的文本
UI.getText(id);                            // 读上述文本
UI.exists(id);                             // id → DisplayObject 是否存在
```

### 5.3 `globalThis.hostArgs: string[]`

由 `Pixi2D.Host` CLI 在 PXML 路径之后收集到的额外位置参数；用来把"目标文件"等参数传给脚本。

```powershell
Pixi2D.Host.exe my-tool.pxml extra1 extra2
# JS:  hostArgs[0] === "extra1"
```

### 5.4 `fs` / `fsAsync`

继承 [qjs.net](https://github.com/lovespite/qjs.net) 默认安装的同步 / 异步文件系统模块；常用：
`fs.readFile(path, 'utf8')`、`fs.writeFile(path, text, 'utf8')`、`fs.stat(path)`（返回含 `mtimeMs` 的对象）。

## v0.6 新增 API

### window 代理

`js
window.title = 'My App';                          // 同步改窗口标题
window.resize(1280, 800);
window.toggleFullScreen();
console.log(window.pxmlPath, window.hostArgs);    // hostArgs 仍可用 globalThis.hostArgs 兼容路径
window.on('resized', (w, h) => console.log(w, h));
window.on('fileChanged', path => console.log('changed', path));   // --watch 模式
window.on('closed', () => console.log('closed'));
``n
### Table 代理

所有 set*Style/setData 方法的 `style/rows` 参数支持直接传 JS 对象/数组（脚本侧自动 JSON.stringify）：

`js
diagTable.hasHeader = true;
diagTable.editMode = 'doubleClick';  // 'none' | 'f2' | 'click' | 'doubleClick'
diagTable.setData([
  ['Severity','Line','Col','Element','Message'],
  ['Error',  '12','5', '<text>', 'invalid color'],
]);
diagTable.setHeaderStyle({ backColor:'#22272e', color:'#cdd9e5', fontSize:12, align:'left' });
diagTable.setRowStyle(1, { backColor:'#3a1f24', color:'#ff6b6b' });    // 错误行红底
diagTable.setColumnStyle(2, { align:'right' });
diagTable.setCellStyle(1, 4, { color:'#ffffff' });
diagTable.clearStyles();
diagTable.on('cellClicked', (row, col, text) => editor.scrollToLine(row));
diagTable.on('rowClicked',  row => console.log('row', row));
``n
样式字段（全部可选）：`backColor` / `color` / `borderColor` (`#RGB`/`#RRGGBB`/`#RRGGBBAA`)；`fontSize` (number)；`align` (`'left'`/`'center'`/`'right'`)。

编辑模式：`editMode` 支持 `none` / `f2` / `click` / `doubleClick`。  
进入编辑后：`Enter` 提交并把焦点移动到同列下一行；`Esc` 取消；失焦自动提交。

#### 增量更新（v0.6.1）

避免高频局部变更走 `setData` 全表重测；行高未变时仅原地刷新已渲染 cell。

```js
diagTable.updateCell(2, 4, 'new message');     // (row, col, value) — 0-based
diagTable.updateRow(2, ['Warn','13','7','<text>','...']);   // 数组自动 JSON 序列化
diagTable.appendRow(['Info','-','-','-','done']);
diagTable.insertRow(0, ['Severity','Line','Col','Element','Message']);
diagTable.removeRow(5);
diagTable.recalculateLayout();                 // 列宽变化时显式触发整表重测
console.log(diagTable.rowCount, diagTable.columnCount);
```

注意：`updateRow` 列数与现有不一致时视为结构变更（走全量）。增量 API 修改的是 Table 内部副本，不会写回原 `setData(...)` 传入的数组。

### TextBox 代理

```js
editor.on('changed', txt => console.log('text now', txt));   // 实时, 替代旧的 setInterval 轮询
editor.scrollToLine(42);                                     // 1-based
editor.setCursorPosition(42, 5);                             // 1-based 行/列;列超过行末夹到 \n 前
editor.selectionStart = 100;
console.log(editor.length, editor.selectionLength);
```

## v0.7 新增：Assets 代理

globalThis.assets 提供资源加载入口（详见 [assets.md](assets.md)）。

### 异步加载（事件驱动）

每个异步方法立刻返回一个 `int requestId`；结果通过 `assets.on('xxx', fn)` 派发。

```js
// 文本
let id1 = assets.loadText('readme.txt');
assets.on('loadedText', (requestId, url, text, metaJson) => {
    if (requestId !== id1) return;
    const meta = JSON.parse(metaJson);
    console.log(text, meta.fromCache);
});

// 二进制 (base64)
let id2 = assets.loadBytes('image.png');
assets.on('loadedBytes', (requestId, url, base64, metaJson) => { /* ... */ });

// JSON (脚本侧 JSON.parse)
let id3 = assets.loadJson('https://api.example.com/data');
assets.on('loadedJson', (requestId, url, jsonText, metaJson) => {
    const obj = JSON.parse(jsonText);
});

// 错误 / 进度
assets.on('error',    (requestId, url, msg) => console.error(url, msg));
assets.on('progress', (requestId, url, loaded, total) => { /* total<0 表示未知 */ });
```

### 同步加载（仅本地）

```js
const text = assets.loadTextSync('config.json');     // HTTP URL → null
const b64  = assets.loadBytesSync('icon.png');
const ok   = assets.exists('config.json');
```

### 缓存控制

```js
assets.clearCache();          // L1 + L2 全清
assets.clearMemoryCache();    // 仅 L1
assets.clearDiskCache();      // 仅 L2
assets.removeCache(url);      // 单条
const stats = JSON.parse(assets.cacheStats());
// { memoryBytes, memoryEntries, diskBytes, diskEntries }
```

### meta 字段

`loadedText/Bytes/Json` 回调最后一个参数是 JSON 字符串：

```json
{
  "source": "https://example.com/x.json",
  "contentType": "application/json",
  "fromCache": false,
  "fetchedAt": "2025-01-01T08:00:00.0000000+08:00",
  "sizeBytes": 1234,
  "statusCode": 200
}
```
