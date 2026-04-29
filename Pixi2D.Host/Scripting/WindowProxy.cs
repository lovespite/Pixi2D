using QuickJsNet.Interop;

namespace Pixi2D.Host.Scripting;

/// <summary>
/// 把宿主窗体（<see cref="PixiHostWindow"/>）以 <c>globalThis.window</c> 暴露给 JS。
/// 命名遵循自动 PascalCase → camelCase 规则。
/// </summary>
[JSExport]
public partial class WindowProxy
{
    private readonly PixiHostWindow _w;
    private readonly string _pxmlPath;
    private readonly string[] _hostArgs;

    /// <summary>窗口大小变化（宽,高）。</summary>
    public event Action<int, int>? Resized;
    /// <summary>窗口被关闭（在 native 关闭之后触发）。</summary>
    public event Action? Closed;
    /// <summary>watch 模式下文件变化通知（绝对路径）。</summary>
    public event Action<string>? FileChanged;

    public WindowProxy(PixiHostWindow w, string pxmlPath, string[] hostArgs)
    {
        _w = w;
        _pxmlPath = pxmlPath;
        _hostArgs = hostArgs ?? Array.Empty<string>();
        _w.Resize += (ww, hh) => Resized?.Invoke(ww, hh);
    }

    internal void RaiseFileChanged(string path) => FileChanged?.Invoke(path);
    internal void RaiseClosed() => Closed?.Invoke();

    public string Title { get => _w.Title; set => _w.SetTitle(value ?? string.Empty); }
    public int Width  { get => _w.Width;  set => _w.SetSize(value, _w.Height); }
    public int Height { get => _w.Height; set => _w.SetSize(_w.Width, value); }
    public bool IsFullScreen
    {
        get => _isFullScreen;
        set { if (value != _isFullScreen) { _w.ToggleFullScreen(); _isFullScreen = value; } }
    }
    private bool _isFullScreen;

    public string PxmlPath => _pxmlPath;
    public string[] HostArgs => _hostArgs;

    public void Close() => _w.Close();
    public void ToggleFullScreen() { _w.ToggleFullScreen(); _isFullScreen = !_isFullScreen; }

    public void SetTitle(string title) => _w.SetTitle(title ?? string.Empty);

    public void Resize(int w, int h) => _w.SetSize(w, h);

    /// <summary>请求一次重绘（兼容 API；Direct2D1Window 默认连续重绘）。</summary>
    public void RequestRedraw() { /* no-op: Direct2D1Window already in continuous render loop */ }
}
