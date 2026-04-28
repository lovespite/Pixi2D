# Pixi2D Scripting (QuickJS)

> 适用版本：v0.3。本文档描述 PXML + JavaScript 集成的设计与使用。

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
