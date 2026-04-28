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

    private readonly Stage _stage = new();
    private QuickJsScriptEngine? _engine;
    private DisplayObject? _root;
    private string? _loadError;
    private FileSystemWatcher? _watcher;
    private DateTime _lastReload = DateTime.MinValue;

    public override string WindowClassName => "Pixi2DHostWindow";

    public PixiHostWindow(string pxmlPath, string? jsPath, bool watch, string title, int w, int h)
        : base(title, w, h)
    {
        _pxmlPath = Path.GetFullPath(pxmlPath);
        _jsPath = jsPath is null ? null : Path.GetFullPath(jsPath);
        _watch = watch;

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
    }

    protected override void OnPaint(RenderTarget target, float deltaTimeInSeconds)
    {
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
}
