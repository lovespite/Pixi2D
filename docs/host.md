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
