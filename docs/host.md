# Pixi2D.Host

> 一个基于 [`D2DWindow`](https://github.com/lovespite/D2DWindow) 的轻量宿主，把 PXML 加载到 `Pixi2D.Stage` 并跑 QuickJS 脚本。

## 1. CLI 用法

```
Pixi2D.Host.exe <foo.pxml> [选项]

选项:
  --script <foo.js>    显式指定脚本; 默认尝试同目录同名 .js
  --no-console         不分配/附加控制台窗口
  --watch              监视 .pxml / .js 变化, 自动热重载场景
  --width <N>          窗口宽度 (默认 1024)
  --height <N>         窗口高度 (默认 720)
  --title <S>          窗口标题
```

## 2. 控制台行为

* 默认启动时优先 `AttachConsole(ATTACH_PARENT_PROCESS)`：从 `cmd.exe` / `pwsh` 启动时直接挂到当前控制台；从资源管理器双击启动时退化为 `AllocConsole`，分配一个独立控制台窗口。
* `Console.OutputEncoding = UTF-8`，便于中文输出。
* JS 端 `console.log/info/warn/error` 会经由 `ConsoleShim` → 宿主回调 → `Console.WriteLine`；
* QuickJS 引擎自身的 `OnLog` 也会以 `[QJS]` / `[QJS WARN]` / `[QJS ERROR]` 前缀写入控制台。
* `--no-console` 完全跳过 console 分配（适用于打包发布、被其它进程嵌入等场景）。

## 3. 启动时序

```
Program.Main
 └─ AllocConsole (除非 --no-console)
 └─ new PixiHostWindow(pxml, js, watch, ...)         ← 创建 D2D 渲染窗口 (基类 ctor 同步触发 OnDeviceReady)
      ├─ OnDeviceReady: 设置 UIContext.DWriteFactory + Stage.SetCachedRenderTarget
      └─ (派生 ctor 完成: 字段就绪)
 └─ window.Run()
      ├─ OnLoad: BuildScene()
      │    ├─ PxmlLoader.LoadFromFile → DisplayObject root
      │    ├─ stage.AddChild(root)
      │    ├─ new QuickJsScriptEngine
      │    ├─ ScriptBootstrap.Install(engine, host, factory) ← 注册 console + 注入命名控件
      │    ├─ engine.Execute(jsSource)                       ← 用户脚本
      │    └─ ScriptBootstrap.ApplyOnAttributes              ← 发射 on-* 胶水
      ├─ (--watch) StartWatcher: FileSystemWatcher + RunOnUIThread 防抖 500ms
      └─ 主循环: PeekMessage + OnPaint(stage.Render)
```

> ⚠️ 重要：`OnDeviceReady` 在基类 ctor 期间同步触发，此时派生类字段尚未赋值；
> 因此 `BuildScene` 必须放在 `OnLoad`（`Run()` 之后）。

## 4. 文件查找约定

* 显式 `--script foo.js`：使用指定路径。
* 否则同目录下与 PXML 同名的 `.js`（`hello.pxml` ↔ `hello.js`）。
* 都不存在则跳过脚本，仅渲染静态 UI。

## 5. 热重载

* `--watch` 启用 `FileSystemWatcher`，监听 PXML 与 JS 的 `Changed/Created` 事件。
* 防抖 500ms；变化后通过 `RunOnUIThread` 编组到 UI 线程，调用 `BuildScene()` 重建场景。
* 重建时 dispose 旧 `QuickJsScriptEngine` 并重新执行用户脚本（保守策略，避免脏状态）。

## 5b. JS 事件循环 Pump (v0.4+)

* **主心跳走 Win32 `WM_TIMER`**：`OnLoad` 注册 `SetTimer(handle, id, 16, NULL)`；窗口的 `WndProc`（`HandleWndProc` override）在收到 `WM_TIMER` 时调用 `_engine.Pump()`。
* `WM_TIMER` 关键属性：在 size/move modal loop（拖动 / 缩放 / 标题栏菜单 / `WM_ENTERSIZEMOVE` 期间）**也会被派发**，所以 `setInterval` / `setTimeout` 在用户操作窗口时**仍然滴答**，不会被冻结。
* 与 `System.Windows.Forms.Timer` 同底层（也是 `WM_TIMER`），但**零 WinForms 依赖**：纯 `LibraryImport` P/Invoke `user32.dll`（AOT 友好，启用 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` 满足 source-gen 要求）。
* 退化策略：若 `SetTimer` 失败（罕见），自动回退到 `OnPaint` 心跳（拖窗会停，但至少基本场景能跑）。
* 回调与渲染同线程（D2D 主消息循环线程），无需 `RunOnUIThread` 编组。
* 最小有效间隔：受 `WM_TIMER` 的 `USER_TIMER_MINIMUM`（10ms）下限钳制；当前固定 16ms (~60Hz)，足以驱动 `setInterval(>=10ms)` 的应用。
* **无累积漂移**：qjs.net `EventLoop` 已采用 cumulative scheduling（`next_deadline = previous_deadline + interval`），`setInterval(100ms)` 长期运行误差收敛到 <1%，与 wall-clock 同步；远超一个 interval 落后（suspend / GC pause / 模态阻塞）时自动 snap-forward 防止 burst 补发。
* Pump 异常被捕获并写到诊断回调，不会中断渲染或 timer 心跳。

## 6. 鼠标按键映射

| D2DWindow `MouseButton` | Pixi2D `Stage.DispatchMouseDown(_, int button)` |
|---|---|
| Left   | 0 |
| Right  | 1 |
| Middle | 2 |
| X1 / X2 | 3 / 4 |

## 7. 已知限制

* 仅 Windows x64（D2DWindow 是 Win32 + 原生 quickjs.dll）。
* 渲染线程 = 主消息循环线程：所有外部回调（`FileSystemWatcher`、网络等）必须 `RunOnUIThread` 编组。
* 错误叠加只是窗口左上角红色 DrawText，未来可加结构化诊断面板。

## 8. v0.5：透传 hostArgs

PXML 路径之后未被识别为选项的所有位置参数会被收集到 `globalThis.hostArgs: string[]`，供 JS 读取。

```powershell
Pixi2D.Host.exe my-tool.pxml --watch arg1 arg2
# 在 my-tool.js 中：
#   hostArgs[0] === "arg1"
#   hostArgs[1] === "arg2"
```

典型用途：[`tools/preview`](../tools/preview/README.md) 用 `hostArgs[0]` 接收要预览的目标 .pxml 路径。

## v0.6 — Window 代理

`globalThis.window` 由 `Pixi2D.Host.Scripting.WindowProxy` 提供，命名遵循 PascalCase → camelCase 规则。脚本可直接读写窗口属性、订阅尺寸/关闭/文件变化事件。详见 `docs/scripting.md`。

