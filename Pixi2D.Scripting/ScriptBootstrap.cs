using System.Text;
using Pixi2D.Core;
using Pixi2D.Markup;
using Pixi2D.Markup.Diagnostics;

namespace Pixi2D.Scripting;

/// <summary>
/// 把 <see cref="ScriptHost"/> 的命名控件与 <c>on-*</c> 事件挂接发布到给定 <see cref="IScriptEngine"/>。<br />
/// 全部走 source-generator 代理 + 字符串拼接，运行时无反射。
/// </summary>
public static class ScriptBootstrap
{
    /// <summary>
    /// 安装顺序约定：
    /// <list type="number">
    ///   <item>注册 <c>console</c>（log/warn/error/info）。</item>
    ///   <item>把每个 <c>id</c> 命名控件包装为代理 → <see cref="IScriptEngine.SetGlobal"/>。</item>
    /// </list>
    /// 之后调用方应执行用户脚本，最后再调 <see cref="ApplyOnAttributes"/>。
    /// </summary>
    public static void Install(
        IScriptEngine engine,
        ScriptHost host,
        IProxyFactory proxyFactory,
        Action<DiagnosticSeverity, string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(proxyFactory);

        ConsoleShim.Install(engine, log);
        BindNamedObjects(engine, host, proxyFactory, log);
    }

    public static void InstallConsoleOnly(IScriptEngine engine, Action<DiagnosticSeverity, string>? log = null)
        => ConsoleShim.Install(engine, log);

    public static void BindNamedObjects(
        IScriptEngine engine,
        ScriptHost host,
        IProxyFactory proxyFactory,
        Action<DiagnosticSeverity, string>? log = null)
    {
        foreach (var (id, control) in host.NamedObjects)
        {
            if (!IsValidJsIdentifier(id))
            {
                log?.Invoke(DiagnosticSeverity.Warning, $"id \"{id}\" 不是合法 JS 标识符；跳过 SetGlobal。");
                continue;
            }
            var proxy = proxyFactory.Create(control);
            if (proxy is null)
            {
                log?.Invoke(DiagnosticSeverity.Info, $"控件类型 {control.GetType().Name} 暂无 JS 代理；id \"{id}\" 不可见于脚本。");
                continue;
            }
            engine.SetGlobal(id, proxy);
        }
    }

    /// <summary>
    /// 在用户脚本之后调用：发射 <c>id.onEvent = handler;</c> 形式的胶水，使 PXML 的 <c>on-*</c> 与 JS 全局函数完成绑定。
    /// </summary>
    public static void ApplyOnAttributes(
        IScriptEngine engine,
        ScriptHost host,
        Action<DiagnosticSeverity, string>? log = null)
    {
        if (host.PendingHandlers.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var ph in host.PendingHandlers)
        {
            var id = ph.Target.Name;
            if (string.IsNullOrEmpty(id) || !IsValidJsIdentifier(id))
            {
                log?.Invoke(DiagnosticSeverity.Warning,
                    $"on-* 绑定要求目标控件具备合法 id；事件 {ph.EventName} → {ph.HandlerName} 已跳过。");
                continue;
            }
            // EventName 已是 PascalCase（来自 PxmlLoader 的 KebabToPascal）；
            // qjs.net 的 [JSExport] 把 C# 事件暴露为 JS 端的 obj.on('camelCase', fn) 形式。
            var jsEventName = ToCamelCase(ph.EventName);
            var handler = ph.HandlerName;
            sb.Append("if (typeof ").Append(handler).Append(" === 'function') ")
              .Append(id).Append(".on('").Append(jsEventName).Append("', ").Append(handler).Append(");\n");
        }

        if (sb.Length == 0) return;
        try { engine.Execute(sb.ToString(), "<pxml-on-attrs>"); }
        catch (Exception ex)
        {
            log?.Invoke(DiagnosticSeverity.Error, $"on-* 胶水脚本执行失败: {ex.Message}");
            throw;
        }
    }

    private static bool IsValidJsIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (!(char.IsLetter(s[0]) || s[0] == '_' || s[0] == '$')) return false;
        for (int i = 1; i < s.Length; i++)
        {
            var c = s[i];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$')) return false;
        }
        return true;
    }

    /// <summary>
    /// 与 qjs.net source generator 同步的 PascalCase → camelCase 转换：
    /// 把开头的连续大写小写化（保留紧邻小写字母前的最后一个大写）。
    /// </summary>
    internal static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new StringBuilder(name.Length);
        int i = 0;
        while (i < name.Length && char.IsUpper(name[i]))
        {
            if (i > 0 && i + 1 < name.Length && char.IsLower(name[i + 1])) break;
            sb.Append(char.ToLowerInvariant(name[i]));
            i++;
        }
        if (i < name.Length) sb.Append(name, i, name.Length - i);
        return sb.ToString();
    }
}
