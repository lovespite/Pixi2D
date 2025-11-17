using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixi2D.Extensions;

public static class ColorExtensions
{
    public static Color ToColor(this RawColor4 rawColor4)
    {
        return Color.FromArgb(
            (int)(rawColor4.A * 255),
            (int)(rawColor4.R * 255),
            (int)(rawColor4.G * 255),
            (int)(rawColor4.B * 255)
        );
    }

    public static RawColor4 ToRawColor4(this Color color)
    {
        return new RawColor4(
            color.R / 255f,
            color.G / 255f,
            color.B / 255f,
            color.A / 255f
        );
    }
}
