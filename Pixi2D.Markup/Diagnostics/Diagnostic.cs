namespace Pixi2D.Markup.Diagnostics;

/// <summary>诊断信息严重程度。</summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// 一条 PXML 诊断信息（解析/加载阶段产生）。<br />
/// 同时被 <see cref="PxmlLoader"/> 用作 warning 收集容器，与抛出的 <see cref="PxmlException"/> 互补。
/// </summary>
public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string Message,
    string? FilePath = null,
    int Line = 0,
    int Column = 0,
    string? ElementName = null,
    string? AttributeName = null)
{
    /// <summary>编译器风格格式：<c>path(line,col): severity: message [&lt;tag&gt; attr=...]</c>。</summary>
    public override string ToString()
    {
        var loc = !string.IsNullOrEmpty(FilePath)
            ? (Line > 0 ? $"{FilePath}({Line},{Column})" : FilePath)
            : (Line > 0 ? $"({Line},{Column})" : "");
        var sev = Severity.ToString().ToLowerInvariant();
        var tag = ElementName is null ? "" : $" <{ElementName}>";
        var attr = AttributeName is null ? "" : $" @{AttributeName}";
        var prefix = string.IsNullOrEmpty(loc) ? "" : loc + ": ";
        return $"{prefix}{sev}:{tag}{attr} {Message}".Trim();
    }
}
