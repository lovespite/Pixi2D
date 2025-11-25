using Pixi2D.Extensions;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Core;


public struct BrushStyle
{
    public bool IsGradient;
    public bool IsRelativePosition;
    public RawColor4 SolidColor;
    public PointF Start;
    public PointF End;
    public GradientStop[] Stops;

    public BrushStyle(RawColor4 color)
    {
        IsGradient = false;
        IsRelativePosition = false;
        SolidColor = color;
        Start = End = PointF.Empty;
        Stops = [];
    }

    public BrushStyle(PointF start, PointF end, GradientStop[] stops)
    {
        IsGradient = true;
        IsRelativePosition = false;
        SolidColor = default;
        Start = start;
        End = end;
        Stops = stops;
    }

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
        // 计算起点和终点
        float radians = degree * (float)(Math.PI / 180);
        PointF center = new(0.5f, 0.5f);
        float halfDiagonal = (float)(Math.Sqrt(2) / 2);
        PointF start = new(
            center.X - halfDiagonal * (float)Math.Cos(radians),
            center.Y - halfDiagonal * (float)Math.Sin(radians)
        );
        PointF end = new(
            center.X + halfDiagonal * (float)Math.Cos(radians),
            center.Y + halfDiagonal * (float)Math.Sin(radians)
        );
        return new BrushStyle(start, end, stops)
        {
            IsRelativePosition = true, // 通过角度设置时使用相对位置
        };
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
}