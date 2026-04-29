using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;

namespace Pixi2D.Controls;

/// <summary>
/// 单元格水平对齐枚举 (与 SharpDX TextAlignment 解耦; 由 Table 渲染时映射)。
/// </summary>
public enum TableHAlign
{
    Left,
    Center,
    Right,
}

/// <summary>
/// 表格样式（可级联合并）。<br />
/// 优先级: Cell &gt; Row &gt; Column &gt; Header(if row=0 &amp;&amp; HasHeader) &gt; DefaultStyle。<br />
/// 所有字段为 nullable —— null 表示「沿用上层」，非 null 则覆盖。
/// </summary>
public sealed class TableStyle
{
    public RawColor4? BackColor { get; set; }
    public RawColor4? Color     { get; set; }
    public RawColor4? BorderColor { get; set; }
    public float? FontSize { get; set; }
    public TableHAlign? HAlign { get; set; }

    /// <summary>把 <paramref name="other"/> 中非空字段覆盖到本对象之上, 返回新实例（不可变合并）。</summary>
    public TableStyle MergeWith(TableStyle? other)
    {
        if (other is null) return this;
        return new TableStyle
        {
            BackColor   = other.BackColor   ?? BackColor,
            Color       = other.Color       ?? Color,
            BorderColor = other.BorderColor ?? BorderColor,
            FontSize    = other.FontSize    ?? FontSize,
            HAlign      = other.HAlign      ?? HAlign,
        };
    }

    /// <summary>把本对象按字段写入 <paramref name="cell"/> ；为 null 的字段保留 cell 默认。</summary>
    internal static TextAlignment ToDWriteAlign(TableHAlign a) => a switch
    {
        TableHAlign.Center => TextAlignment.Center,
        TableHAlign.Right  => TextAlignment.Trailing,
        _                  => TextAlignment.Leading,
    };
}
