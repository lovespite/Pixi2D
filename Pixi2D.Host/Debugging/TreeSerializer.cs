using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using Pixi2D.Core;

namespace Pixi2D.Host.Debugging;

/// <summary>把 <see cref="Stage"/> 序列化为元素树 JSON，并维护 id↔节点反查表。</summary>
internal static class TreeSerializer
{
    private static int _autoId;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DisplayObject, object> _idMap = new();
    private static readonly ConcurrentDictionary<int, WeakReference<DisplayObject>> _byId = new();

    public static int IdOf(DisplayObject d)
    {
        if (_idMap.TryGetValue(d, out var v)) return (int)v!;
        var id = System.Threading.Interlocked.Increment(ref _autoId);
        _idMap.Add(d, id);
        _byId[id] = new WeakReference<DisplayObject>(d);
        return id;
    }

    public static DisplayObject? Resolve(int id)
        => _byId.TryGetValue(id, out var wr) && wr.TryGetTarget(out var d) ? d : null;

    public static JsonObject Serialize(Stage stage)
        => new JsonObject { ["root"] = SerializeNode(stage) };

    private static JsonObject SerializeNode(DisplayObject d)
    {
        var node = new JsonObject
        {
            ["id"]          = IdOf(d),
            ["kind"]        = d.GetType().Name,
            ["name"]        = d.Name,
            ["x"]           = d.X,
            ["y"]           = d.Y,
            ["w"]           = d.Width,
            ["h"]           = d.Height,
            ["scaleX"]      = d.ScaleX,
            ["scaleY"]      = d.ScaleY,
            ["rotation"]    = d.Rotation,
            ["alpha"]       = d.Alpha,
            ["anchorX"]     = d.AnchorX,
            ["anchorY"]     = d.AnchorY,
            ["visible"]     = d.Visible,
            ["interactive"] = d.Interactive,
            ["acceptFocus"] = d.AcceptFocus,
        };
        if (d is Container c)
        {
            var arr = new JsonArray();
            foreach (var child in c) arr.Add(SerializeNode(child));
            node["children"] = arr;
        }
        return node;
    }

    /// <summary>白名单属性 setter（必须在 UI 线程调用）。</summary>
    public static void SetProperty(DisplayObject d, string name, JsonNode? value)
    {
        switch (name)
        {
            case "X":           d.X = AsFloat(value); break;
            case "Y":           d.Y = AsFloat(value); break;
            case "Width":       d.Width = AsFloat(value); break;
            case "Height":      d.Height = AsFloat(value); break;
            case "Alpha":       d.Alpha = AsFloat(value); break;
            case "Rotation":    d.Rotation = AsFloat(value); break;
            case "ScaleX":      d.ScaleX = AsFloat(value); break;
            case "ScaleY":      d.ScaleY = AsFloat(value); break;
            case "AnchorX":     d.AnchorX = AsFloat(value); break;
            case "AnchorY":     d.AnchorY = AsFloat(value); break;
            case "Visible":     d.Visible = AsBool(value); break;
            case "Interactive": d.Interactive = AsBool(value); break;
            case "AcceptFocus": d.AcceptFocus = AsBool(value); break;
            case "Name":        d.Name = value?.GetValue<string>() ?? ""; break;
            default: throw new ArgumentException($"property '{name}' not in writable whitelist");
        }
    }

    private static float AsFloat(JsonNode? v)
    {
        if (v is null) throw new ArgumentException("value is null");
        if (v is JsonValue jv && jv.TryGetValue<double>(out var d)) return (float)d;
        return float.Parse(v.ToString(), CultureInfo.InvariantCulture);
    }

    private static bool AsBool(JsonNode? v)
    {
        if (v is null) throw new ArgumentException("value is null");
        if (v is JsonValue jv && jv.TryGetValue<bool>(out var b)) return b;
        return bool.Parse(v.ToString());
    }
}
