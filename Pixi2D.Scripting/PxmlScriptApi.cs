using Pixi2D.Components;
using Pixi2D.Controls;
using Pixi2D.Core;
using Pixi2D.Markup;
using Pixi2D.Markup.Diagnostics;

namespace Pixi2D.Scripting;

/// <summary>
/// 把 Preview / 工具脚本所需的 PXML 反射 + 容器操作以一组 <c>globalThis</c> 函数 / 对象暴露给 JS。<br />
/// 这一层有意只用<b>基础类型</b>（string / double / bool）+ JSON 跨边界，避免给每个控件单独写 [JSExport] 代理。
/// </summary>
public static class PxmlScriptApi
{
    /// <summary>
    /// 安装 <c>globalThis.Pxml</c> + <c>globalThis.UI</c>。
    /// </summary>
    /// <param name="engine">QuickJS 适配器（或任意 <see cref="IScriptEngine"/>）。</param>
    /// <param name="host">PxmlLoader 关联的 ScriptHost；用于按 id 反查 DisplayObject。</param>
    public static void Install(IScriptEngine engine, ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(host);

        // ── Pxml.parse(text, virtualPath?) → JSON 字符串 ─────────────────────────────
        engine.RegisterFunction("__pixi_pxml_parse", args =>
        {
            var text = args.Length > 0 ? args[0] as string ?? string.Empty : string.Empty;
            var virt = args.Length > 1 ? args[1] as string : null;
            return ParseToJson(text, virt);
        });

        // ── UI.clear(id) ───────────────────────────────────────────────────────────
        engine.RegisterFunction("__pixi_ui_clear", args =>
        {
            var id = args.Length > 0 ? args[0] as string : null;
            if (id is null || !host.NamedObjects.TryGetValue(id, out var obj)) return false;
            return ClearChildren(obj);
        });

        // ── UI.appendText(id, text, color?, fontSize?) ─────────────────────────────
        engine.RegisterFunction("__pixi_ui_append_text", args =>
        {
            var id = args.Length > 0 ? args[0] as string : null;
            var text = args.Length > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
            var color = args.Length > 2 ? args[2] as string : null;
            double? size = args.Length > 3 && args[3] is not null && double.TryParse(args[3]!.ToString(), out var s) ? s : null;
            if (id is null || !host.NamedObjects.TryGetValue(id, out var obj)) return false;
            return AppendText(obj, text, color, size);
        });

        // ── UI.setText(id, text)  /  UI.getText(id) ────────────────────────────────
        engine.RegisterFunction("__pixi_ui_set_text", args =>
        {
            var id = args.Length > 0 ? args[0] as string : null;
            var text = args.Length > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;
            if (id is null || !host.NamedObjects.TryGetValue(id, out var obj)) return false;
            return SetTextLike(obj, text);
        });
        engine.RegisterFunction("__pixi_ui_get_text", args =>
        {
            var id = args.Length > 0 ? args[0] as string : null;
            if (id is null || !host.NamedObjects.TryGetValue(id, out var obj)) return null;
            return GetTextLike(obj);
        });

        // ── UI.exists(id) ──────────────────────────────────────────────────────────
        engine.RegisterFunction("__pixi_ui_exists", args =>
        {
            var id = args.Length > 0 ? args[0] as string : null;
            return id is not null && host.NamedObjects.ContainsKey(id);
        });

        // ── JS 胶水：把上面的低级函数包装成 Pxml / UI ──────────────────────────────
        engine.Execute(@"
globalThis.Pxml = globalThis.Pxml || {};
Pxml.parse = function(text, virtualPath) {
    var json = __pixi_pxml_parse(String(text || ''), virtualPath || null);
    try { return JSON.parse(json); }
    catch (e) {
        return { ok: false, diagnostics: [{ severity: 'Error', line: 0, column: 0, message: 'Pxml.parse 内部 JSON 异常: ' + e.message }], tree: [] };
    }
};
globalThis.UI = globalThis.UI || {};
UI.clear       = function(id) { return !!__pixi_ui_clear(String(id)); };
UI.appendText  = function(id, text, color, size) { return !!__pixi_ui_append_text(String(id), String(text == null ? '' : text), color || null, size == null ? null : Number(size)); };
UI.setText     = function(id, text) { return !!__pixi_ui_set_text(String(id), text == null ? '' : String(text)); };
UI.getText     = function(id) { return __pixi_ui_get_text(String(id)); };
UI.exists      = function(id) { return !!__pixi_ui_exists(String(id)); };
", "<pxml-script-api>");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Pxml.parse 实现
    // ──────────────────────────────────────────────────────────────────────────────

    private static string ParseToJson(string text, string? virtualPath)
    {
        var loader = new PxmlLoader();
        try
        {
            var root = loader.LoadFromString(text, virtualPath);
            var tree = new List<TreeNodeDto>();
            WalkTree(root, depth: 0, tree);
            return BuildJson(ok: true, loader.Diagnostics.Select(ToDto).ToList(), tree);
        }
        catch (PxmlException pex)
        {
            var diags = loader.Diagnostics.Select(ToDto).ToList();
            diags.Add(new DiagnosticDto
            {
                Severity = "Error",
                Line = pex.Line,
                Column = pex.Column,
                Element = pex.ElementName,
                Attribute = pex.AttributeName,
                Message = pex.Message,
            });
            return BuildJson(ok: false, diags, new List<TreeNodeDto>());
        }
        catch (Exception ex)
        {
            return BuildJson(ok: false, new List<DiagnosticDto>
            {
                new() { Severity = "Error", Message = ex.Message }
            }, new List<TreeNodeDto>());
        }
    }

    // 手写 JSON：避免 System.Text.Json 反射 → AOT 友好。
    private static string BuildJson(bool ok, List<DiagnosticDto> diags, List<TreeNodeDto> tree)
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", ok);
            w.WriteStartArray("diagnostics");
            foreach (var d in diags)
            {
                w.WriteStartObject();
                w.WriteString("severity", d.Severity);
                w.WriteNumber("line", d.Line);
                w.WriteNumber("column", d.Column);
                if (d.Element is null) w.WriteNull("element"); else w.WriteString("element", d.Element);
                if (d.Attribute is null) w.WriteNull("attribute"); else w.WriteString("attribute", d.Attribute);
                w.WriteString("message", d.Message);
                if (d.File is null) w.WriteNull("file"); else w.WriteString("file", d.File);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("tree");
            foreach (var n in tree)
            {
                w.WriteStartObject();
                w.WriteNumber("depth", n.Depth);
                w.WriteString("type", n.Type);
                w.WriteString("id", n.Id);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WalkTree(DisplayObject obj, int depth, List<TreeNodeDto> sink)
    {
        sink.Add(new TreeNodeDto
        {
            Depth = depth,
            Type = obj.GetType().Name,
            Id = obj.Name ?? string.Empty,
        });
        if (obj is Container c)
        {
            foreach (var child in c) WalkTree(child, depth + 1, sink);
        }
    }

    private static DiagnosticDto ToDto(Diagnostic d) => new()
    {
        Severity = d.Severity.ToString(),
        Line = d.Line,
        Column = d.Column,
        Element = d.ElementName,
        Attribute = d.AttributeName,
        Message = d.Message,
        File = d.FilePath,
    };

    // ──────────────────────────────────────────────────────────────────────────────
    // UI.* 实现：清空 / 追加文本
    // ──────────────────────────────────────────────────────────────────────────────

    private static bool ClearChildren(DisplayObject obj)
    {
        switch (obj)
        {
            case Panel panel: panel.ClearContent(); return true;
            case Container container: container.ClearChildren(); return true;
            default: return false;
        }
    }

    private static bool AppendText(DisplayObject obj, string text, string? color, double? fontSize)
    {
        var fancy = new FancyText { Content = text };
        if (fontSize.HasValue) fancy.FontSize = (float)fontSize.Value;
        if (!string.IsNullOrEmpty(color))
        {
            try { fancy.TextColor = ParseColor(color); }
            catch { /* 颜色解析失败时静默回落 */ }
        }
        // 简单纵向堆叠：根据当前子项数 × 行高定位 Y
        float lineHeight = fancy.FontSize + 4f;
        int existing;
        if (obj is Panel panel) existing = panel.ContentContainer.Count;
        else if (obj is Container c) existing = c.Count;
        else return false;
        fancy.Y = existing * lineHeight + 4f;
        fancy.X = 6f;

        if (obj is Panel p) { p.AddContent(fancy); return true; }
        if (obj is Container co) { co.AddChild(fancy); return true; }
        return false;
    }

    private static SharpDX.Mathematics.Interop.RawColor4 ParseColor(string s)
    {
        // 只接受 #RGB / #RRGGBB / #RRGGBBAA
        if (s.StartsWith('#')) s = s[1..];
        byte r, g, b, a = 255;
        if (s.Length == 3)
        {
            r = (byte)(Convert.ToByte(new string(s[0], 2), 16));
            g = (byte)(Convert.ToByte(new string(s[1], 2), 16));
            b = (byte)(Convert.ToByte(new string(s[2], 2), 16));
        }
        else if (s.Length == 6 || s.Length == 8)
        {
            r = Convert.ToByte(s[..2], 16);
            g = Convert.ToByte(s.Substring(2, 2), 16);
            b = Convert.ToByte(s.Substring(4, 2), 16);
            if (s.Length == 8) a = Convert.ToByte(s.Substring(6, 2), 16);
        }
        else throw new FormatException("color must be #RGB / #RRGGBB / #RRGGBBAA");
        return new SharpDX.Mathematics.Interop.RawColor4(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    private static bool SetTextLike(DisplayObject obj, string text)
    {
        switch (obj)
        {
            case FancyText ft: ft.Content = text; return true;
            case Text t: t.Content = text; return true;
            case TextBox tb: tb.Text = text; return true;
            case Button btn: btn.Text = text; return true;
            default: return false;
        }
    }

    private static string? GetTextLike(DisplayObject obj) => obj switch
    {
        FancyText ft => ft.Content,
        Text t => t.Content,
        TextBox tb => tb.Text,
        Button btn => btn.Text,
        _ => null,
    };

    // ──────────────────────────────────────────────────────────────────────────────
    // DTO (内部容器, 用于手写 JSON; 不走反射序列化)
    // ──────────────────────────────────────────────────────────────────────────────

    private sealed class DiagnosticDto
    {
        public string Severity { get; set; } = "Info";
        public int Line { get; set; }
        public int Column { get; set; }
        public string? Element { get; set; }
        public string? Attribute { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? File { get; set; }
    }

    private sealed class TreeNodeDto
    {
        public int Depth { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
    }
}
