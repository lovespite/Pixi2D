using Pixi2D.Markup;
using Pixi2D.Markup.Diagnostics;

namespace Pixi2D.Scripting;

/// <summary>
/// 在脚本引擎上注册 <c>console</c>（log/warn/error/info）。<br />
/// 通过 <see cref="IScriptEngine.RegisterFunction"/>（基础类型）+ JS 胶水构造 <c>globalThis.console</c>。
/// </summary>
public static class ConsoleShim
{
    public static void Install(IScriptEngine engine, Action<DiagnosticSeverity, string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(engine);

        engine.RegisterFunction("__pixi_log_info", args =>
        {
            var msg = Format(args);
            log?.Invoke(DiagnosticSeverity.Info, msg);
            Console.Out.WriteLine(msg);
            return null;
        });
        engine.RegisterFunction("__pixi_log_warn", args =>
        {
            var msg = Format(args);
            log?.Invoke(DiagnosticSeverity.Warning, msg);
            Console.Out.WriteLine("WARN  " + msg);
            return null;
        });
        engine.RegisterFunction("__pixi_log_error", args =>
        {
            var msg = Format(args);
            log?.Invoke(DiagnosticSeverity.Error, msg);
            Console.Error.WriteLine("ERROR " + msg);
            return null;
        });

        engine.Execute(
            "globalThis.console = globalThis.console || {};" +
            "console.log   = function() { __pixi_log_info.apply(null, arguments);  };" +
            "console.info  = function() { __pixi_log_info.apply(null, arguments);  };" +
            "console.warn  = function() { __pixi_log_warn.apply(null, arguments);  };" +
            "console.error = function() { __pixi_log_error.apply(null, arguments); };",
            "<console-shim>");
    }

    private static string Format(object?[] args)
    {
        if (args.Length == 0) return string.Empty;
        if (args.Length == 1) return args[0]?.ToString() ?? "null";
        return string.Join(' ', args.Select(a => a?.ToString() ?? "null"));
    }
}
