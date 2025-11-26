using Pixi2D.Extensions;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Core;


public struct BrushStyle
{
    public bool IsGradient;
    public bool IsAngleGradient;
    public RawColor4 SolidColor;
    public PointF Start;
    public PointF End;
    public GradientStop[] Stops;
    public float GradientAngle;

    /// <summary>
    /// Initializes a new instance of the BrushStyle class with a solid color.
    /// </summary>
    /// <remarks>The created BrushStyle instance represents a solid color brush. Gradient-related properties
    /// are set to their default values and are not used for this constructor.</remarks>
    /// <param name="color">The color to use for the solid brush. Specifies the RGBA value that defines the brush's fill color.</param>
    public BrushStyle(RawColor4 color)
    {
        IsGradient = false;
        IsAngleGradient = false;
        SolidColor = color;
        Start = End = PointF.Empty;
        Stops = [];
    }

    public BrushStyle(Color color) : this(color.ToRawColor4()) { }

    /// <summary>
    /// Initializes a new instance of the BrushStyle class that defines a linear gradient brush with the specified start
    /// and end points and gradient stops.
    /// </summary>
    /// <remarks>Use this constructor to create a linear gradient brush for rendering graphics with smooth
    /// color transitions. The gradient is defined by the provided start and end points, and the color distribution is
    /// determined by the specified gradient stops.</remarks>
    /// <param name="start">The starting point of the gradient, specified in coordinates relative to the drawing surface.</param>
    /// <param name="end">The ending point of the gradient, specified in coordinates relative to the drawing surface.</param>
    /// <param name="stops">An array of GradientStop objects that define the colors and positions along the gradient. Cannot be null or
    /// empty.</param>
    public BrushStyle(PointF start, PointF end, GradientStop[] stops)
    {
        IsGradient = true;
        IsAngleGradient = false;
        SolidColor = default;
        Start = start;
        End = end;
        Stops = stops;
    }

    /// <summary>
    /// Initializes a new instance of the BrushStyle class using an angle-based gradient and the specified gradient
    /// stops.
    /// </summary>
    /// <remarks>Use this constructor to create a BrushStyle that renders a gradient at a specific angle. The
    /// gradient is defined by the provided stops, which determine the color transitions along the gradient
    /// direction.</remarks>
    /// <param name="angleDegrees">The angle, in degrees, at which the gradient is applied. Typically, 0 degrees represents a horizontal gradient.</param>
    /// <param name="stops">An array of GradientStop objects that define the colors and positions within the gradient. Cannot be null or
    /// empty.</param>
    public BrushStyle(float angleDegrees, GradientStop[] stops)
    {
        IsGradient = true;
        IsAngleGradient = true;
        SolidColor = default;
        Start = End = PointF.Empty;
        Stops = stops;
        GradientAngle = angleDegrees;
    }

    /// <summary>
    /// 核心计算方法：根据传入的矩形范围，计算出实际的线性渐变起止点
    /// </summary>
    /// <param name="bounds">图形的实际绘制区域 (World Bounds)</param>
    /// <returns>绝对坐标系下的 Start 和 End</returns>
    public readonly (RawVector2 Start, RawVector2 End) GetLinearGradientVectors(ref RectangleF bounds)
    {
        if (!IsGradient) return (default, default);

        float x = bounds.Left;
        float y = bounds.Top;
        float w = bounds.Width;
        float h = bounds.Height;

        // 模式 A: 基于角度的动态计算 (类似 CSS)
        // 保证渐变线穿过中心，且能覆盖矩形四角
        if (IsAngleGradient)
        {
            // 1. 转弧度 (假设 0度=向右, 顺时针)
            float angleRad = GradientAngle * (float)(Math.PI / 180.0);

            // 2. 计算中心点
            float cx = x + w / 2.0f;
            float cy = y + h / 2.0f;

            // 3. 计算方向向量
            float dirX = (float)Math.Cos(angleRad);
            float dirY = (float)Math.Sin(angleRad);

            // 4. 计算覆盖半径 (投影长度)
            // 原理：计算矩形从中心到最远角落，在渐变方向上的投影长度
            // 这样保证无论旋转多少度，渐变色都能恰好填满盒子，不会出现断层
            float halfLength = (Math.Abs(w * dirX) + Math.Abs(h * dirY)) / 2.0f;

            // 5. 计算起止点
            return (
                new RawVector2(cx - dirX * halfLength, cy - dirY * halfLength),
                new RawVector2(cx + dirX * halfLength, cy + dirY * halfLength)
            );
        }

        // 模式 B: 基于相对坐标点的映射
        return (
            new RawVector2(x + Start.X, y + Start.Y),
            new RawVector2(x + End.X, y + End.Y)
        );
    }

    #region Factory Methods

    /// <summary>
    /// Creates a linear gradient brush style oriented at the specified angle, using the provided colors as gradient
    /// stops.
    /// </summary>
    /// <remarks>The gradient is centered and sized relative to the unit square, with positions specified as
    /// relative coordinates. The method uses evenly spaced gradient stops based on the order of colors in the array. If
    /// fewer than two colors are provided, the resulting gradient may not display as expected.</remarks>
    /// <param name="degree">The angle, in degrees, at which the gradient is oriented. Measured clockwise from the horizontal axis.</param>
    /// <param name="colors">An array of colors to use as gradient stops. The colors are distributed evenly along the gradient line. Must
    /// contain at least two elements.</param>
    /// <returns>A BrushStyle instance representing a linear gradient at the specified angle with the given colors.</returns>
    public static BrushStyle LinearGradient(float degree, params Color[] colors)
    {
        var stops = new GradientStop[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            stops[i] = new GradientStop
            {
                Position = (float)i / (colors.Length - 1),
                Color = colors[i].ToRawColor4()
            };
        }
        return new BrushStyle(degree, stops);
    }

    public static BrushStyle LinearGradient(PointF start, PointF end, params Color[] colors)
    {
        var stops = new GradientStop[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            stops[i] = new GradientStop
            {
                Position = (float)i / (colors.Length - 1),
                Color = colors[i].ToRawColor4()
            };
        }
        return new BrushStyle(start, end, stops);
    }

    public static BrushStyle LinearGradient(PointF start, PointF end, Color startColor, Color endColor)
    {
        var stops = new GradientStop[2];
        stops[0] = new GradientStop
        {
            Position = 0f,
            Color = startColor.ToRawColor4()
        };
        stops[1] = new GradientStop
        {
            Position = 1f,
            Color = endColor.ToRawColor4()
        };
        return new BrushStyle(start, end, stops);
    }

    public static BrushStyle LinearGradient(PointF start, PointF end, params GradientStop[] stops)
    {
        return new BrushStyle(start, end, stops);
    }

    public static BrushStyle LinearGradient(float startX, float startY, float endX, float endY, params Color[] colors)
    {
        return LinearGradient(new PointF(startX, startY), new PointF(endX, endY), colors);
    }


    public static BrushStyle LinearGradient(float startX, float startY, float endX, float endY, Color startColor, Color endColor)
    {
        return LinearGradient(new PointF(startX, startY), new PointF(endX, endY), startColor, endColor);
    }

    public static BrushStyle LinearGradient(float startX, float startY, float endX, float endY, params GradientStop[] stops)
    {
        return new BrushStyle(new PointF(startX, startY), new PointF(endX, endY), stops);
    }

    #endregion
}