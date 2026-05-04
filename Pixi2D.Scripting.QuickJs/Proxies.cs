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
    public event Action<string>? Changed;

    public TextBoxProxy(TextBox tb)
    {
        _tb = tb;
        _tb.TextChanged += (_, s) => Changed?.Invoke(s);
    }
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

    public int SelectionStart { get => _tb.SelectionStart; set => _tb.SelectionStart = value; }
    public int SelectionLength { get => _tb.SelectionLength; set => _tb.SelectionLength = value; }
    public int Length => _tb.Length;

    public void ScrollToLine(int line) => _tb.ScrollToLine(line);
    public void ScrollToCaret() => _tb.ScrollToCaret();
    public void ScrollToTop() => _tb.ScollToTop();
    public void ScrollToBottom() => _tb.ScrollToBottom();
    public void SelectAll() => _tb.SelectAll();
    public void SetCursorPosition(int line, int column) => _tb.SetCursorPosition(line, column);
    public void Focus() => _tb.Focus();
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

/// <summary>
/// 颜色 / 对齐 / 字号样式参数。<br />
/// 颜色字符串格式: <c>#RGB</c> / <c>#RRGGBB</c> / <c>#RRGGBBAA</c>; 解析失败时该字段视为 null。
/// align 取值: "left" / "center" / "right"。
/// </summary>
internal static class TableStyleJson
{
    public static TableStyle? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            var s = new TableStyle();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                switch (prop.Name)
                {
                    case "backColor":   s.BackColor   = TryParseColor(prop.Value.GetString()); break;
                    case "color":       s.Color       = TryParseColor(prop.Value.GetString()); break;
                    case "borderColor": s.BorderColor = TryParseColor(prop.Value.GetString()); break;
                    case "fontSize":
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number && prop.Value.TryGetDouble(out var d))
                            s.FontSize = (float)d;
                        break;
                    case "align": s.HAlign = ParseAlign(prop.Value.GetString()); break;
                }
            }
            return s;
        }
        catch { return null; }
    }

    public static string[][] ParseRows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string[]>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return Array.Empty<string[]>();
            var rows = new List<string[]>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.ValueKind != System.Text.Json.JsonValueKind.Array) { rows.Add(Array.Empty<string>()); continue; }
                var cells = new List<string>();
                foreach (var c in row.EnumerateArray())
                {
                    cells.Add(c.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => c.GetString() ?? string.Empty,
                        System.Text.Json.JsonValueKind.Null => string.Empty,
                        _ => c.GetRawText(),
                    });
                }
                rows.Add(cells.ToArray());
            }
            return rows.ToArray();
        }
        catch { return Array.Empty<string[]>(); }
    }

    public static string[] ParseRow(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return Array.Empty<string>();
            var cells = new List<string>();
            foreach (var c in doc.RootElement.EnumerateArray())
            {
                cells.Add(c.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => c.GetString() ?? string.Empty,
                    System.Text.Json.JsonValueKind.Null => string.Empty,
                    _ => c.GetRawText(),
                });
            }
            return cells.ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    private static TableHAlign? ParseAlign(string? a) => (a?.ToLowerInvariant()) switch
    {
        "left"   => TableHAlign.Left,
        "center" => TableHAlign.Center,
        "right"  => TableHAlign.Right,
        _ => null,
    };

    private static SharpDX.Mathematics.Interop.RawColor4? TryParseColor(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.StartsWith('#') ? s[1..] : s;
        try
        {
            byte r, g, b, a = 255;
            if (t.Length == 3)
            {
                r = Convert.ToByte(new string(t[0], 2), 16);
                g = Convert.ToByte(new string(t[1], 2), 16);
                b = Convert.ToByte(new string(t[2], 2), 16);
            }
            else if (t.Length == 6 || t.Length == 8)
            {
                r = Convert.ToByte(t[..2], 16);
                g = Convert.ToByte(t.Substring(2, 2), 16);
                b = Convert.ToByte(t.Substring(4, 2), 16);
                if (t.Length == 8) a = Convert.ToByte(t.Substring(6, 2), 16);
            }
            else return null;
            return new SharpDX.Mathematics.Interop.RawColor4(r / 255f, g / 255f, b / 255f, a / 255f);
        }
        catch { return null; }
    }
}

[JSExport]
public partial class TableProxy : IControlProxy, IJsonShimProxy
{
    private static readonly IReadOnlyList<(string Method, int[] JsonArgIndices)> _shims = new (string, int[])[]
    {
        ("setData",        new[] { 0 }),
        ("setTableStyle",  new[] { 0 }),
        ("setHeaderStyle", new[] { 0 }),
        ("setRowStyle",    new[] { 1 }),
        ("setColumnStyle", new[] { 1 }),
        ("setCellStyle",   new[] { 2 }),
        ("updateRow",      new[] { 1 }),
        ("appendRow",      new[] { 0 }),
        ("insertRow",      new[] { 1 }),
    };
    IReadOnlyList<(string Method, int[] JsonArgIndices)> IJsonShimProxy.ShimMethods => _shims;

    private readonly Table _t;
    /// <summary>单元格点击 (row, col, text)。</summary>
    public event Action<int, int, string>? CellClicked;
    /// <summary>整行点击 (row)。</summary>
    public event Action<int>? RowClicked;

    public TableProxy(Table t)
    {
        _t = t;
        _t.CellClicked += (r, c, txt) => CellClicked?.Invoke(r, c, txt ?? string.Empty);
        _t.RowClicked  += r => RowClicked?.Invoke(r);
    }
    DisplayObject IControlProxy.Wrapped => _t;

    public string Id { get => _t.Name ?? string.Empty; set => _t.Name = value; }
    public float X { get => _t.X; set => _t.X = value; }
    public float Y { get => _t.Y; set => _t.Y = value; }
    public float Width { get => _t.Width; set => _t.Width = value; }
    public float Height { get => _t.Height; set => _t.Height = value; }
    public bool Visible { get => _t.Visible; set => _t.Visible = value; }

    public bool HasHeader { get => _t.HasHeader; set => _t.HasHeader = value; }
    public string EditMode
    {
        get => _t.EditMode.ToString();
        set
        {
            if (!Enum.TryParse<TableEditMode>(value, true, out var mode))
                throw new ArgumentException($"Invalid table edit mode: {value}", nameof(value));
            _t.EditMode = mode;
        }
    }
    public int RowCount    => _t.RowCount;
    public int ColumnCount => _t.ColumnCount;

    /// <summary>整表数据 (JSON 字符串 <c>[["a","b"],...]</c>)。脚本侧通过 setData(rows) shim 自动 JSON.stringify。</summary>
    public void SetData(string rowsJson)
    {
        _t.DataSource = TableStyleJson.ParseRows(rowsJson);
        _t.NotifyDataChanged();
    }

    public void Clear()
    {
        _t.DataSource = Array.Empty<string[]>();
        _t.NotifyDataChanged();
    }

    public void SetTableStyle(string styleJson)              => _t.SetTableStyle(TableStyleJson.Parse(styleJson));
    public void SetHeaderStyle(string styleJson)             => _t.SetHeaderStyle(TableStyleJson.Parse(styleJson));
    public void SetRowStyle(int row, string styleJson)       => _t.SetRowStyle(row, TableStyleJson.Parse(styleJson));
    public void SetColumnStyle(int col, string styleJson)    => _t.SetColumnStyle(col, TableStyleJson.Parse(styleJson));
    public void SetCellStyle(int row, int col, string styleJson) => _t.SetCellStyle(row, col, TableStyleJson.Parse(styleJson));
    public void ClearStyles() => _t.ClearStyles();

    // ---------- 增量更新 (避免 setData 全量重测) ----------
    /// <summary>修改单元格内容 (row/col 均 0-based)。</summary>
    public void UpdateCell(int row, int col, string value) => _t.UpdateCell(row, col, value);
    /// <summary>覆盖整行: cellsJson 形如 <c>["a","b","c"]</c>。</summary>
    public void UpdateRow(int row, string cellsJson) => _t.UpdateRow(row, TableStyleJson.ParseRow(cellsJson));
    /// <summary>末尾追加一行: cellsJson 形如 <c>["a","b","c"]</c>。</summary>
    public void AppendRow(string cellsJson) => _t.AppendRow(TableStyleJson.ParseRow(cellsJson));
    /// <summary>在 row 位置插入一行 (row 0-based)。</summary>
    public void InsertRow(int row, string cellsJson) => _t.InsertRow(row, TableStyleJson.ParseRow(cellsJson));
    /// <summary>移除一行。</summary>
    public void RemoveRow(int row) => _t.RemoveRow(row);
    /// <summary>显式触发整表重测算 (列宽 / 行高 / 总尺寸)。</summary>
    public void RecalculateLayout() => _t.NotifyDataChanged();
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
