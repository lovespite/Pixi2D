using Pixi2D.Components;
using Pixi2D.Controls;
using Pixi2D.Core;
using QuickJsNet.Interop;

namespace Pixi2D.Scripting.QuickJs;

// 设计说明：
//  - 每个控件一个 [JSExport] partial class 代理；运行时无反射，全部由 QuickJsNet.SourceGenerators 生成绑定器。
//  - JS 端命名规则由生成器决定：C# PascalCase → JS camelCase；事件统一通过 obj.on('eventName', fn) / obj.off(...) 订阅。
//  - 所有事件签名只用基础类型（无自定义 class 引用），避免回调时跨边界 marshalling 不确定。
//  - 通用 DisplayObject 属性（id / x / y / width / height / visible）在每个代理里手写转发；不继承公共基类以避免与 partial 冲突。

internal static class ProxyHelpers
{
    public static void CopyDOProps(DisplayObject d, out string id, out float x, out float y, out float w, out float h, out bool visible)
    {
        id = d.Name ?? string.Empty;
        x = d.X; y = d.Y; w = d.Width; h = d.Height; visible = d.Visible;
    }
}

[JSExport]
public partial class DisplayObjectProxy : IControlProxy
{
    private readonly DisplayObject _d;
    public DisplayObjectProxy(DisplayObject d) { _d = d; }
    DisplayObject IControlProxy.Wrapped => _d;

    public string Id { get => _d.Name ?? string.Empty; set => _d.Name = value; }
    public float X { get => _d.X; set => _d.X = value; }
    public float Y { get => _d.Y; set => _d.Y = value; }
    public float Width { get => _d.Width; set => _d.Width = value; }
    public float Height { get => _d.Height; set => _d.Height = value; }
    public bool Visible { get => _d.Visible; set => _d.Visible = value; }
}

[JSExport]
public partial class ButtonProxy : IControlProxy
{
    private readonly Button _b;
    public event Action? Click;

    public ButtonProxy(Button b)
    {
        _b = b;
        _b.OnButtonClick += _ => Click?.Invoke();
    }
    DisplayObject IControlProxy.Wrapped => _b;

    public string Id { get => _b.Name ?? string.Empty; set => _b.Name = value; }
    public float X { get => _b.X; set => _b.X = value; }
    public float Y { get => _b.Y; set => _b.Y = value; }
    public float Width { get => _b.Width; set => _b.Width = value; }
    public float Height { get => _b.Height; set => _b.Height = value; }
    public bool Visible { get => _b.Visible; set => _b.Visible = value; }

    public string Text { get => _b.Text; set => _b.Text = value; }
}

[JSExport]
public partial class FancyTextProxy : IControlProxy
{
    private readonly FancyText _t;
    public FancyTextProxy(FancyText t) { _t = t; }
    DisplayObject IControlProxy.Wrapped => _t;

    public string Id { get => _t.Name ?? string.Empty; set => _t.Name = value; }
    public float X { get => _t.X; set => _t.X = value; }
    public float Y { get => _t.Y; set => _t.Y = value; }
    public float Width { get => _t.Width; set => _t.Width = value; }
    public float Height { get => _t.Height; set => _t.Height = value; }
    public bool Visible { get => _t.Visible; set => _t.Visible = value; }

    public string Text { get => _t.Content; set => _t.Content = value; }
    public string Content { get => _t.Content; set => _t.Content = value; }
}

[JSExport]
public partial class TextBoxProxy : IControlProxy
{
    private readonly TextBox _tb;
    public TextBoxProxy(TextBox tb) { _tb = tb; }
    DisplayObject IControlProxy.Wrapped => _tb;

    public string Id { get => _tb.Name ?? string.Empty; set => _tb.Name = value; }
    public float X { get => _tb.X; set => _tb.X = value; }
    public float Y { get => _tb.Y; set => _tb.Y = value; }
    public float Width { get => _tb.Width; set => _tb.Width = value; }
    public float Height { get => _tb.Height; set => _tb.Height = value; }
    public bool Visible { get => _tb.Visible; set => _tb.Visible = value; }

    public string Text { get => _tb.Text; set => _tb.Text = value; }
    public string Value { get => _tb.Text; set => _tb.Text = value; }
    public string Placeholder { get => _tb.PlaceholderText; set => _tb.PlaceholderText = value; }
    public bool ReadOnly { get => _tb.ReadOnly; set => _tb.ReadOnly = value; }
}

[JSExport]
public partial class SwitchProxy : IControlProxy
{
    private readonly Switch _s;
    public event Action<bool>? Changed;

    public SwitchProxy(Switch s)
    {
        _s = s;
        _s.OnChanged += (_, v) => Changed?.Invoke(v);
    }
    DisplayObject IControlProxy.Wrapped => _s;

    public string Id { get => _s.Name ?? string.Empty; set => _s.Name = value; }
    public float X { get => _s.X; set => _s.X = value; }
    public float Y { get => _s.Y; set => _s.Y = value; }
    public float Width { get => _s.Width; set => _s.Width = value; }
    public float Height { get => _s.Height; set => _s.Height = value; }
    public bool Visible { get => _s.Visible; set => _s.Visible = value; }

    public bool IsOn { get => _s.IsOn; set => _s.IsOn = value; }
    public bool Checked { get => _s.IsOn; set => _s.IsOn = value; }
}

[JSExport]
public partial class NumberProxy : IControlProxy
{
    private readonly Number _n;
    public NumberProxy(Number n) { _n = n; }
    DisplayObject IControlProxy.Wrapped => _n;

    public string Id { get => _n.Name ?? string.Empty; set => _n.Name = value; }
    public float X { get => _n.X; set => _n.X = value; }
    public float Y { get => _n.Y; set => _n.Y = value; }
    public float Width { get => _n.Width; set => _n.Width = value; }
    public float Height { get => _n.Height; set => _n.Height = value; }
    public bool Visible { get => _n.Visible; set => _n.Visible = value; }

    public double Value { get => (double)_n.Value; set => _n.Value = (decimal)value; }
    public string Format { get => _n.Format; set => _n.Format = value; }
    public string Prefix { get => _n.Prefix; set => _n.Prefix = value; }
    public string Suffix { get => _n.Suffix; set => _n.Suffix = value; }
}

[JSExport]
public partial class ProgressBarProxy : IControlProxy
{
    private readonly ProgressBar _p;
    public ProgressBarProxy(ProgressBar p) { _p = p; }
    DisplayObject IControlProxy.Wrapped => _p;

    public string Id { get => _p.Name ?? string.Empty; set => _p.Name = value; }
    public float X { get => _p.X; set => _p.X = value; }
    public float Y { get => _p.Y; set => _p.Y = value; }
    public float Width { get => _p.Width; set => _p.Width = value; }
    public float Height { get => _p.Height; set => _p.Height = value; }
    public bool Visible { get => _p.Visible; set => _p.Visible = value; }

    public double Value { get => _p.Value; set => _p.Value = (float)value; }
}

[JSExport]
public partial class PanelProxy : IControlProxy
{
    private readonly Panel _panel;
    public PanelProxy(Panel p) { _panel = p; }
    DisplayObject IControlProxy.Wrapped => _panel;

    public string Id { get => _panel.Name ?? string.Empty; set => _panel.Name = value; }
    public float X { get => _panel.X; set => _panel.X = value; }
    public float Y { get => _panel.Y; set => _panel.Y = value; }
    public float Width { get => _panel.Width; set => _panel.Width = value; }
    public float Height { get => _panel.Height; set => _panel.Height = value; }
    public bool Visible { get => _panel.Visible; set => _panel.Visible = value; }
}

[JSExport]
public partial class ContainerProxy : IControlProxy
{
    private readonly Container _c;
    public ContainerProxy(Container c) { _c = c; }
    DisplayObject IControlProxy.Wrapped => _c;

    public string Id { get => _c.Name ?? string.Empty; set => _c.Name = value; }
    public float X { get => _c.X; set => _c.X = value; }
    public float Y { get => _c.Y; set => _c.Y = value; }
    public float Width { get => _c.Width; set => _c.Width = value; }
    public float Height { get => _c.Height; set => _c.Height = value; }
    public bool Visible { get => _c.Visible; set => _c.Visible = value; }
}

[JSExport]
public partial class ModalProxy : IControlProxy
{
    private readonly Modal _m;
    public ModalProxy(Modal m) { _m = m; }
    DisplayObject IControlProxy.Wrapped => _m;

    public string Id { get => _m.Name ?? string.Empty; set => _m.Name = value; }
    public bool Visible { get => _m.Visible; set => _m.Visible = value; }
    public void Show() { _m.Visible = true; }
    public void Hide() { _m.Visible = false; }
}
