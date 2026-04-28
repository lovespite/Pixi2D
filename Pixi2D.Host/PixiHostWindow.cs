using System.Drawing;
using System.Runtime.InteropServices;
using D2DWindow;
using Pixi2D.Core;
using Pixi2D.Markup;
using Pixi2D.Markup.Diagnostics;
using Pixi2D.Scripting;
using Pixi2D.Scripting.QuickJs;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

namespace Pixi2D.Host;

/// <summary>
/// PXML + JS 宿主窗口：把 D2DWindow 的事件/渲染管线接到 Pixi2D.Stage，并管理 QuickJS 脚本生命周期。
/// </summary>
public sealed class PixiHostWindow : Direct2D1Window
{
    private readonly string _pxmlPath;
    private readonly string? _jsPath;
    private readonly bool _watch;
    private readonly string[] _extraArgs;

    private readonly Stage _stage = new();
    private QuickJsScriptEngine? _engine;
    private DisplayObject? _root;
    private string? _loadError;
    private FileSystemWatcher? _watcher;
    private DateTime _lastReload = DateTime.MinValue;

    // WM_TIMER pump：与渲染解耦，让 setInterval 在拖窗 / 缩放 / 模态循环里也能滴答。
    private const nuint PumpTimerId = 0x1D01;
    private const uint  PumpTimerIntervalMs = 16; // ~60Hz；WM_TIMER 受 USER_TIMER_MINIMUM(10ms) 下限钳制
    private bool _pumpTimerInstalled;

    public override string WindowClassName => "Pixi2DHostWindow";

    public PixiHostWindow(string pxmlPath, string? jsPath, bool watch, string title, int w, int h, string[]? extraArgs = null)
        : base(title, w, h)
    {
        _pxmlPath = Path.GetFullPath(pxmlPath);
        _jsPath = jsPath is null ? null : Path.GetFullPath(jsPath);
        _watch = watch;
        _extraArgs = extraArgs ?? Array.Empty<string>();

        BackgroundColor = new RawColor4(0.10f, 0.10f, 0.12f, 1f);

        MouseMove   += e => _stage.DispatchMouseMove(new PointF(e.X, e.Y));
        MouseDown   += e => _stage.DispatchMouseDown(new PointF(e.X, e.Y), MapButton(e.Button));
        MouseUp     += e => _stage.DispatchMouseUp(new PointF(e.X, e.Y), MapButton(e.Button));
        MouseWheel  += e => _stage.DispatchMouseWheel(new PointF(e.X, e.Y), e.WheelDelta);
        KeyDown     += e => _stage.DispatchKeyDown(e.Key, e.Control, e.Alt, e.Shift);
        KeyUp       += e => _stage.DispatchKeyUp(e.Key, e.Control, e.Alt, e.Shift);
        KeyPress    += e => _stage.DispatchKeyPress(e.KeyChar);
    }

    protected override void OnDeviceReady(RenderTarget target)
    {
        _stage.SetCachedRenderTarget(target);

        // 初始化全局 UIContext (DWriteFactory 等); 首次进入时构造默认 Text.Factory。
        UIContext.Current.DWriteFactory ??= new SharpDX.DirectWrite.Factory();
        _ = UIContext.Current.DefaultTextFactory;
        // 注意: 该回调发生在基类 ctor 期间, 此时派生类字段尚未赋值;
        // 实际场景构建必须推迟到 OnLoad (Run() 之后)。
    }

    protected override void OnLoad()
    {
        BuildScene();
        if (_watch) StartWatcher();

        // 注册 WM_TIMER 驱动 JS 事件循环；与渲染解耦，避免拖窗/缩放时 setInterval 被冻住。
        if (Handle != IntPtr.Zero)
        {
            var id = HostNative.SetTimer(Handle, PumpTimerId, PumpTimerIntervalMs, IntPtr.Zero);
            _pumpTimerInstalled = id != 0;
            if (!_pumpTimerInstalled)
            {
                LogDiagnostic(DiagnosticSeverity.Warning,
                    $"[host] SetTimer 失败 (Win32Error={Marshal.GetLastWin32Error()}); 退化到 OnPaint 心跳");
            }
        }
    }

    protected override IntPtr HandleWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == HostNative.WM_TIMER && (nuint)wParam == PumpTimerId)
        {
            PumpEngine();
            return IntPtr.Zero;
        }
        return base.HandleWndProc(hWnd, msg, wParam, lParam);
    }

    private void PumpEngine()
    {
        if (_engine is null) return;
        try { _engine.Pump(); }
        catch (Exception ex) { LogDiagnostic(DiagnosticSeverity.Error, "[pump] " + ex.Message); }
    }

    protected override void OnPaint(RenderTarget target, float deltaTimeInSeconds)
    {
        // 主心跳走 WM_TIMER (HandleWndProc); 这里仅在 timer 未注册成功时兜底,
        // 确保即便 SetTimer 失败 setInterval 仍能跟随渲染帧滴答 (拖窗会停).
        if (!_pumpTimerInstalled) PumpEngine();

        _stage.Render(target);

        if (_loadError is not null)
        {
            // 简易错误叠加：在左上角红色文字
            using var brush = new SolidColorBrush(target, new RawColor4(1f, 0.2f, 0.2f, 1f));
            using var dwf = new SharpDX.DirectWrite.Factory();
            using var fmt = new SharpDX.DirectWrite.TextFormat(dwf, "Consolas", 14f);
            target.DrawText(_loadError, fmt, new RawRectangleF(8, 8, Width - 8, Height - 8), brush);
        }
    }

    private void BuildScene()
    {
        try
        {
            DisposeScene();

            var loader = new PxmlLoader();
            _root = loader.LoadFromFile(_pxmlPath);
            _stage.AddChild(_root);

            // 把 loader 的 Diagnostics 写到控制台
            foreach (var d in loader.Diagnostics)
                Console.WriteLine($"[{d.Severity}] {d}");

            // 创建脚本引擎
            _engine = new QuickJsScriptEngine();
            _engine.OnLog += (lvl, msg) =>
            {
                var prefix = lvl switch { 1 => "[QJS WARN] ", 2 => "[QJS ERROR] ", _ => "[QJS] " };
                Console.WriteLine(prefix + msg);
            };

            var factory = new QuickJsProxyFactory();
            ScriptBootstrap.Install(_engine, loader.Host, factory, LogDiagnostic);

            // Preview / 工具脚本所需的 PXML 解析 + 容器操作 API（globalThis.Pxml / UI）。
            PxmlScriptApi.Install(_engine, loader.Host);

            // 把宿主命令行中 PXML 之后的额外位置参数以 string[] 形式暴露给 JS。
            _engine.Execute("globalThis.hostArgs = " + BuildJsStringArray(_extraArgs) + ";", "<host-args>");

            // 加载 JS:  <script src> 暂未实现解析, 这里走 jsPath/同名 .js
            var jsFile = ResolveJs();
            if (jsFile is not null && File.Exists(jsFile))
            {
                var src = File.ReadAllText(jsFile);
                _engine.Execute(src, jsFile);
                Console.WriteLine($"[host] 已加载脚本: {jsFile}");
            }
            else
            {
                Console.WriteLine("[host] 未找到 JS 脚本; 仅运行 PXML.");
            }

            ScriptBootstrap.ApplyOnAttributes(_engine, loader.Host, LogDiagnostic);
            _loadError = null;
        }
        catch (Exception ex)
        {
            _loadError = ex.ToString();
            Console.Error.WriteLine("[host] 场景构建失败:");
            Console.Error.WriteLine(ex);
        }
    }

    private static void LogDiagnostic(DiagnosticSeverity sev, string msg)
        => Console.WriteLine($"[bootstrap {sev}] {msg}");

    private string? ResolveJs()
    {
        if (_jsPath is not null) return _jsPath;
        var byConvention = Path.ChangeExtension(_pxmlPath, ".js");
        return File.Exists(byConvention) ? byConvention : null;
    }

    private void DisposeScene()
    {
        if (_root is not null) { _stage.RemoveChild(_root); _root = null; }
        _engine?.Dispose();
        _engine = null;
    }

    private void StartWatcher()
    {
        var dir = Path.GetDirectoryName(_pxmlPath);
        if (dir is null) return;
        _watcher = new FileSystemWatcher(dir) { EnableRaisingEvents = true, IncludeSubdirectories = false };
        _watcher.Filters.Add("*.pxml");
        _watcher.Filters.Add("*.js");
        FileSystemEventHandler handler = (_, e) =>
        {
            if (e.FullPath != _pxmlPath && e.FullPath != ResolveJs()) return;
            // 防抖 500ms
            var now = DateTime.UtcNow;
            if ((now - _lastReload).TotalMilliseconds < 500) return;
            _lastReload = now;
            RunOnUIThread(() =>
            {
                Console.WriteLine($"[host] 文件变化, 重建场景: {Path.GetFileName(e.FullPath)}");
                BuildScene();
            });
        };
        _watcher.Changed += handler;
        _watcher.Created += handler;
    }

    public override void Dispose()
    {
        if (_pumpTimerInstalled && Handle != IntPtr.Zero)
        {
            HostNative.KillTimer(Handle, PumpTimerId);
            _pumpTimerInstalled = false;
        }
        _watcher?.Dispose();
        DisposeScene();
        base.Dispose();
    }

    private static int MapButton(MouseButton b) => b switch
    {
        MouseButton.Left => 0,
        MouseButton.Right => 1,
        MouseButton.Middle => 2,
        MouseButton.X1 => 3,
        MouseButton.X2 => 4,
        _ => 0,
    };

    /// <summary>构造一段 JS 字符串字面量数组（避免引入 JSON 反射）。</summary>
    private static string BuildJsStringArray(string[] items)
    {
        if (items.Length == 0) return "[]";
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeJs(items[i]));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string EscapeJs(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("X4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
