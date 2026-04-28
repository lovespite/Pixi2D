using System.Xml;
using System.Xml.Linq;
using Pixi2D.Components;
using Pixi2D.Controls;
using Pixi2D.Core;
using Pixi2D.Markup.Diagnostics;

namespace Pixi2D.Markup;

/// <summary>
/// PXML (Pixi2D XML) 加载器。<br />
/// 将 .pxml 文档解析为 <see cref="DisplayObject"/> 树，并在 <see cref="Diagnostics"/> 中收集警告/信息。
/// </summary>
public sealed class PxmlLoader
{
    private readonly ScriptHost _host;
    private string? _currentFile;

    public PxmlLoader(ScriptHost? host = null)
    {
        _host = host ?? new ScriptHost();
    }

    public ScriptHost Host => _host;

    /// <summary>本次 Load 收集到的所有诊断信息（不含已抛出的异常本身）。</summary>
    public List<Diagnostic> Diagnostics { get; } = new();

    public DisplayObject LoadFromString(string xml, string? virtualPath = null)
    {
        Diagnostics.Clear();
        _currentFile = virtualPath;
        XDocument doc;
        try { doc = XDocument.Parse(xml, LoadOptions.SetLineInfo); }
        catch (XmlException ex) { throw new PxmlParseException(ex, _currentFile); }
        return LoadFromDocument(doc);
    }

    public DisplayObject LoadFromFile(string path)
    {
        Diagnostics.Clear();
        _currentFile = path;
        XDocument doc;
        try
        {
            using var fs = File.OpenRead(path);
            doc = XDocument.Load(fs, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex) { throw new PxmlParseException(ex, _currentFile); }
        return LoadFromDocument(doc);
    }

    public DisplayObject LoadFromDocument(XDocument doc)
    {
        if (doc.Root is null)
            throw new PxmlParseException(new XmlException("PXML 文档没有根元素。"), _currentFile);

        var root = doc.Root;
        if (string.Equals(root.Name.LocalName, "ui", StringComparison.Ordinal))
        {
            var children = root.Elements().ToArray();
            if (children.Length == 1) return BuildElement(children[0]);
            var container = new Container();
            foreach (var child in children) container.AddChild(BuildElement(child));
            return container;
        }
        return BuildElement(root);
    }

    private DisplayObject BuildElement(XElement element)
    {
        var name = element.Name.LocalName;
        var (line, col) = GetPos(element);

        if (ElementRegistry.Resolve(name) is null && !ElementRegistry.HasFactory(name))
            throw new PxmlUnknownElementException(name, _currentFile, line, col);

        var instance = ElementRegistry.Create(name);

        ApplyAttributes(instance, element);
        ApplyContent(instance, element);
        ApplyChildren(instance, element);

        return instance;
    }

    private void ApplyAttributes(DisplayObject instance, XElement element)
    {
        var type = instance.GetType();
        var (elemLine, elemCol) = GetPos(element);
        var elemName = element.Name.LocalName;

        foreach (var attr in element.Attributes())
        {
            var attrName = attr.Name.LocalName;
            var value = attr.Value;
            var (line, col) = GetPos(attr, elemLine, elemCol);

            if (attrName.Equals("id", StringComparison.Ordinal) || attrName.Equals("name", StringComparison.Ordinal))
            {
                instance.Name = value;
                _host.NamedObjects[value] = instance;
                continue;
            }

            if (attrName.StartsWith("on-", StringComparison.Ordinal))
            {
                var eventName = ValueConverters.KebabToPascal(attrName[3..]);
                _host.PendingHandlers.Add(new ScriptHost.PendingHandler(instance, eventName, value));
                continue;
            }

            var prop = ValueConverters.FindProperty(type, attrName);
            if (prop is null || !prop.CanWrite)
            {
                Diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    $"未知或不可写的属性 \"{attrName}\" (类型 {type.Name})；已忽略。",
                    _currentFile, line, col, elemName, attrName));
                continue;
            }

            try
            {
                var converted = ValueConverters.Convert(value, prop.PropertyType);
                prop.SetValue(instance, converted);
            }
            catch (Exception ex)
            {
                throw new PxmlAttributeException(
                    elemName, attrName, value, prop.PropertyType,
                    $"无法把 \"{value}\" 转换为 {prop.PropertyType.Name}: {ex.Message}",
                    _currentFile, line, col, ex);
            }
        }
    }

    private void ApplyContent(DisplayObject instance, XElement element)
    {
        var inner = element.Nodes().OfType<XText>().FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(inner)) return;
        if (instance is Text text) { text.Content = inner; return; }

        var (line, col) = GetPos(element);
        Diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Warning,
            $"<{element.Name.LocalName}> 的内文 \"{Truncate(inner)}\" 被忽略 ({instance.GetType().Name} 不接受文本内容)。",
            _currentFile, line, col, element.Name.LocalName));
    }

    private void ApplyChildren(DisplayObject parent, XElement element)
    {
        foreach (var childElement in element.Elements())
        {
            var child = BuildElement(childElement);
            AppendChild(parent, child, element.Name.LocalName, childElement);
        }
    }

    private void AppendChild(DisplayObject parent, DisplayObject child, string parentElement, XElement childElement)
    {
        switch (parent)
        {
            case Panel panel: panel.AddContent(child); return;
            case ListItem li: li.AddContent(child); return;
            case Container container: container.AddChild(child); return;
        }
        var (line, col) = GetPos(childElement);
        throw new PxmlStructureException(parentElement, childElement.Name.LocalName, _currentFile, line, col);
    }

    private static (int line, int col) GetPos(IXmlLineInfo info)
        => info.HasLineInfo() ? (info.LineNumber, info.LinePosition) : (0, 0);

    private static (int line, int col) GetPos(XAttribute attr, int fallbackLine, int fallbackCol)
    {
        var info = (IXmlLineInfo)attr;
        return info.HasLineInfo() ? (info.LineNumber, info.LinePosition) : (fallbackLine, fallbackCol);
    }

    private static string Truncate(string s, int max = 40)
        => s.Length <= max ? s : s[..max] + "...";
}
