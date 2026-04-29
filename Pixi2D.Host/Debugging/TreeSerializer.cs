using System.Text.Json.Nodes;
using Pixi2D.Core;

namespace Pixi2D.Host.Debugging;

/// <summary>把 <see cref="Stage"/> 序列化为元素树 JSON (供 Debugger 显示)。</summary>
internal static class TreeSerializer
{
    private static int _autoId;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DisplayObject, object> _idMap = new();

    public static int IdOf(DisplayObject d)
    {
        if (_idMap.TryGetValue(d, out var v)) return (int)v!;
        var id = System.Threading.Interlocked.Increment(ref _autoId);
        _idMap.Add(d, id);
        return id;
    }

    public static JsonObject Serialize(Stage stage)
    {
        return new JsonObject
        {
            ["root"] = SerializeNode(stage),
        };
    }

    private static JsonObject SerializeNode(DisplayObject d)
    {
        var node = new JsonObject
        {
            ["id"]      = IdOf(d),
            ["kind"]    = d.GetType().Name,
            ["name"]    = d.Name,
            ["x"]       = d.X,
            ["y"]       = d.Y,
            ["w"]       = d.Width,
            ["h"]       = d.Height,
            ["visible"] = d.Visible,
        };
        if (d is Container c)
        {
            var arr = new JsonArray();
            foreach (var child in c)
                arr.Add(SerializeNode(child));
            node["children"] = arr;
        }
        return node;
    }
}
