using System.Reflection;
using System.Xml.Linq;
using Pixi2D.Components;
using Pixi2D.Controls;
using Pixi2D.Core;

namespace Pixi2D.Markup;

/// <summary>
/// PXML (Pixi2D XML) 加载器。<br />
/// 将 .pxml 文档解析为 <see cref="DisplayObject"/> 树。
/// </summary>
/// <remarks>
/// 设计要点：
/// <list type="bullet">
/// <item>元素名与控件类型映射通过 <see cref="ElementRegistry"/> 完成。</item>
/// <item>属性名 kebab-case；自动映射到 PascalCase 公开属性。</item>
/// <item>子元素自动追加：Panel/ListItem 调用 AddContent；其余 Container 调用 AddChild。</item>
/// <item>id 属性会同时设置 <see cref="DisplayObject.Name"/> 并加入 <see cref="ScriptHost.NamedObjects"/>。</item>
/// <item>on-* 属性收集进 <see cref="ScriptHost.PendingHandlers"/>，由后续脚本引擎注入时绑定。</item>
/// <item>内容文本：&lt;text&gt;hello&lt;/text&gt; 会把内文写入 Text.Content (若存在)。</item>
/// </list>
/// </remarks>
public sealed class PxmlLoader
{
    private readonly ScriptHost _host;

    public PxmlLoader(ScriptHost? host = null)
    {
        _host = host ?? new ScriptHost();
    }

    public ScriptHost Host => _host;

    /// <summary>
    /// 从字符串加载根 DisplayObject。
    /// </summary>
    public DisplayObject LoadFromString(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);
        return LoadFromDocument(doc);
    }

    /// <summary>
    /// 从文件加载根 DisplayObject。
    /// </summary>
    public DisplayObject LoadFromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var doc = XDocument.Load(fs, LoadOptions.SetLineInfo);
        return LoadFromDocument(doc);
    }

    public DisplayObject LoadFromDocument(XDocument doc)
    {
        if (doc.Root is null)
            throw new InvalidOperationException("PXML 文档没有根元素。");
        var root = doc.Root;
        // <ui> 包装：单子节点直接返回；多子节点构造 Container。
        if (string.Equals(root.Name.LocalName, "ui", StringComparison.Ordinal))
        {
            var children = root.Elements().ToArray();
            if (children.Length == 1)
                return BuildElement(children[0]);
            var container = new Container();
            foreach (var child in children)
                container.AddChild(BuildElement(child));
            return container;
        }
        return BuildElement(root);
    }

    private DisplayObject BuildElement(XElement element)
    {
        var name = element.Name.LocalName;
        var instance = ElementRegistry.Create(name);

        ApplyAttributes(instance, element);
        ApplyContent(instance, element);
        ApplyChildren(instance, element);

        return instance;
    }

    private void ApplyAttributes(DisplayObject instance, XElement element)
    {
        var type = instance.GetType();
        foreach (var attr in element.Attributes())
        {
            var attrName = attr.Name.LocalName;
            var value = attr.Value;

            // 命名 / 脚本绑定
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
                continue; // 静默忽略未知属性，便于向前兼容

            try
            {
                var converted = ValueConverters.Convert(value, prop.PropertyType);
                prop.SetValue(instance, converted);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"PXML <{element.Name.LocalName}> 属性 \"{attrName}=\\\"{value}\\\"\" 转换为 {prop.PropertyType.Name} 失败: {ex.Message}", ex);
            }
        }
    }

    private static void ApplyContent(DisplayObject instance, XElement element)
    {
        if (instance is Text text)
        {
            var inner = element.Nodes().OfType<XText>().FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(inner))
                text.Content = inner;
        }
    }

    private void ApplyChildren(DisplayObject parent, XElement element)
    {
        foreach (var childElement in element.Elements())
        {
            var child = BuildElement(childElement);
            AppendChild(parent, child);
        }
    }

    private static void AppendChild(DisplayObject parent, DisplayObject child)
    {
        switch (parent)
        {
            case Panel panel:
                panel.AddContent(child);
                return;
            case ListItem li:
                li.AddContent(child);
                return;
            case Container container:
                container.AddChild(child);
                return;
        }
        throw new InvalidOperationException(
            $"控件 {parent.GetType().Name} 不支持子节点。");
    }
}
