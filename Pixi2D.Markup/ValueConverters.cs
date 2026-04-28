using System.Drawing;
using System.Globalization;
using System.Reflection;
using SharpDX.Mathematics.Interop;

namespace Pixi2D.Markup;

/// <summary>
/// PXML 字符串值 → .NET 类型 转换器集合。<br />
/// 内置常见类型 (基本类型、枚举、<see cref="RawColor4"/>、<see cref="SizeF"/>、<see cref="PointF"/>) 的转换；
/// 通过 <see cref="Register"/> 可扩展新类型。
/// </summary>
public static class ValueConverters
{
    private static readonly Dictionary<Type, Func<string, object?>> s_converters = new();

    static ValueConverters()
    {
        Register(typeof(string), v => v);
        Register(typeof(bool), v => bool.Parse(v));
        Register(typeof(int), v => int.Parse(v, CultureInfo.InvariantCulture));
        Register(typeof(long), v => long.Parse(v, CultureInfo.InvariantCulture));
        Register(typeof(float), v => float.Parse(v, CultureInfo.InvariantCulture));
        Register(typeof(double), v => double.Parse(v, CultureInfo.InvariantCulture));
        Register(typeof(decimal), v => decimal.Parse(v, CultureInfo.InvariantCulture));
        Register(typeof(byte), v => byte.Parse(v, CultureInfo.InvariantCulture));
        Register(typeof(RawColor4), v => ParseColor(v));
        Register(typeof(SizeF), v =>
        {
            var p = SplitTwo(v);
            return new SizeF(p.Item1, p.Item2);
        });
        Register(typeof(PointF), v =>
        {
            var p = SplitTwo(v);
            return new PointF(p.Item1, p.Item2);
        });
        Register(typeof(System.Drawing.Color), v =>
        {
            var c = ParseColor(v);
            return Color.FromArgb((int)(c.A * 255), (int)(c.R * 255), (int)(c.G * 255), (int)(c.B * 255));
        });
    }

    public static void Register(Type targetType, Func<string, object?> converter)
        => s_converters[targetType] = converter;

    /// <summary>
    /// 将字符串值转换为目标类型。<br />
    /// 优先匹配已注册的转换器；其次处理可空类型；再次使用枚举名解析；最后退回到 <see cref="Convert.ChangeType(object?, Type, IFormatProvider?)"/>。
    /// </summary>
    public static object? Convert(string value, Type targetType)
    {
        if (s_converters.TryGetValue(targetType, out var conv))
            return conv(value);

        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying is not null)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return Convert(value, underlying);
        }

        if (targetType.IsEnum)
            return Enum.Parse(targetType, value, ignoreCase: true);

        return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// kebab-case → PascalCase。例如 "background-color" → "BackgroundColor"。
    /// </summary>
    public static string KebabToPascal(string kebab)
    {
        if (string.IsNullOrEmpty(kebab)) return kebab;
        var parts = kebab.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    /// <summary>
    /// 在指定类型上查找 PXML 属性名对应的 PropertyInfo。<br />
    /// 同时尝试 kebab→Pascal 转换。返回 null 表示未找到。
    /// </summary>
    public static PropertyInfo? FindProperty(Type type, string attrName)
    {
        var pascal = KebabToPascal(attrName);
        return type.GetProperty(pascal, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    }

    private static (float, float) SplitTwo(string v)
    {
        var parts = v.Split([',', 'x', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new FormatException($"无效的 二元值 \"{v}\"，期望 \"w,h\" 或 \"w h\"。");
        return (float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 解析颜色字符串。支持：
    /// <list type="bullet">
    /// <item><c>#RGB</c> / <c>#RGBA</c> / <c>#RRGGBB</c> / <c>#RRGGBBAA</c></item>
    /// <item><c>rgb(r,g,b)</c> 各分量 0-255</item>
    /// <item><c>rgba(r,g,b,a)</c> a 取 0-1</item>
    /// <item>已知颜色名 (System.Drawing.Color)</item>
    /// </list>
    /// </summary>
    public static RawColor4 ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new RawColor4(0, 0, 0, 0);

        value = value.Trim();
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            byte r, g, b; byte a = 255;
            switch (hex.Length)
            {
                case 3:
                    r = (byte)(HexNibble(hex[0]) * 17);
                    g = (byte)(HexNibble(hex[1]) * 17);
                    b = (byte)(HexNibble(hex[2]) * 17);
                    break;
                case 4:
                    r = (byte)(HexNibble(hex[0]) * 17);
                    g = (byte)(HexNibble(hex[1]) * 17);
                    b = (byte)(HexNibble(hex[2]) * 17);
                    a = (byte)(HexNibble(hex[3]) * 17);
                    break;
                case 6:
                    r = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber);
                    break;
                case 8:
                    r = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber);
                    a = byte.Parse(hex.AsSpan(6, 2), NumberStyles.HexNumber);
                    break;
                default:
                    throw new FormatException($"无效的颜色 \"{value}\"。");
            }
            return new RawColor4(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = value.IndexOf('(');
            var close = value.IndexOf(')');
            if (open < 0 || close < 0)
                throw new FormatException($"无效的颜色 \"{value}\"。");
            var parts = value[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3 || parts.Length > 4)
                throw new FormatException($"无效的颜色 \"{value}\"。");
            float r = float.Parse(parts[0], CultureInfo.InvariantCulture) / 255f;
            float g = float.Parse(parts[1], CultureInfo.InvariantCulture) / 255f;
            float b = float.Parse(parts[2], CultureInfo.InvariantCulture) / 255f;
            float a = parts.Length == 4 ? float.Parse(parts[3], CultureInfo.InvariantCulture) : 1f;
            return new RawColor4(r, g, b, a);
        }

        // 颜色名
        var named = Color.FromName(value);
        if (named.A != 0 || string.Equals(value, "Transparent", StringComparison.OrdinalIgnoreCase))
            return new RawColor4(named.R / 255f, named.G / 255f, named.B / 255f, named.A / 255f);

        throw new FormatException($"无法识别的颜色 \"{value}\"。");
    }

    private static int HexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => throw new FormatException($"无效的十六进制字符 '{c}'。"),
    };
}
