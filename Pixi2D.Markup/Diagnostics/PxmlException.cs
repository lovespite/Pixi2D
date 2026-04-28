using System.Xml;

namespace Pixi2D.Markup.Diagnostics;

/// <summary>
/// PXML 加载阶段所有异常的基类。<br />
/// 携带源位置 (FilePath/Line/Column) 与上下文 (ElementName/AttributeName)，
/// <see cref="ToString"/> 输出编译器风格的标准错误路径，方便 IDE / 预览器双击定位。
/// </summary>
public abstract class PxmlException : Exception
{
    public string? FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public string? ElementName { get; }
    public string? AttributeName { get; }

    protected PxmlException(
        string message,
        string? filePath,
        int line,
        int column,
        string? elementName,
        string? attributeName,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
        ElementName = elementName;
        AttributeName = attributeName;
    }

    public Diagnostic ToDiagnostic() => new(
        DiagnosticSeverity.Error,
        Message,
        FilePath,
        Line,
        Column,
        ElementName,
        AttributeName);

    public override string ToString()
    {
        var loc = !string.IsNullOrEmpty(FilePath)
            ? (Line > 0 ? $"{FilePath}({Line},{Column})" : FilePath)
            : (Line > 0 ? $"({Line},{Column})" : "");
        var tag = ElementName is null ? "" : $" <{ElementName}>";
        var attr = AttributeName is null ? "" : $" @{AttributeName}";
        var prefix = string.IsNullOrEmpty(loc) ? "" : loc + ": ";
        var inner = InnerException is null ? "" : $"  ---> {InnerException.GetType().Name}: {InnerException.Message}";
        return $"{prefix}{GetType().Name}:{tag}{attr} {Message}{inner}".Trim();
    }
}

/// <summary>XML 文档本身解析失败 (语法错误)。</summary>
public sealed class PxmlParseException : PxmlException
{
    public PxmlParseException(XmlException inner, string? filePath)
        : base(inner.Message, filePath, inner.LineNumber, inner.LinePosition, null, null, inner) { }
}

/// <summary>未注册的元素名 (<see cref="ElementRegistry"/> 中找不到)。</summary>
public sealed class PxmlUnknownElementException : PxmlException
{
    public PxmlUnknownElementException(string elementName, string? filePath, int line, int column)
        : base($"未知的 PXML 元素 <{elementName}>。请通过 ElementRegistry.Register / RegisterFactory 注册。",
               filePath, line, column, elementName, null) { }
}

/// <summary>属性值解析或赋值失败。</summary>
public sealed class PxmlAttributeException : PxmlException
{
    public string AttributeValue { get; }
    public Type? TargetType { get; }

    public PxmlAttributeException(
        string elementName,
        string attributeName,
        string attributeValue,
        Type? targetType,
        string message,
        string? filePath,
        int line,
        int column,
        Exception? inner = null)
        : base(message, filePath, line, column, elementName, attributeName, inner)
    {
        AttributeValue = attributeValue;
        TargetType = targetType;
    }
}

/// <summary>子节点不被父元素接受 (例如向非 Container 添加子节点)。</summary>
public sealed class PxmlStructureException : PxmlException
{
    public PxmlStructureException(string parentElement, string childElement, string? filePath, int line, int column)
        : base($"控件 <{parentElement}> 不支持子节点 <{childElement}>。",
               filePath, line, column, parentElement, null) { }
}
