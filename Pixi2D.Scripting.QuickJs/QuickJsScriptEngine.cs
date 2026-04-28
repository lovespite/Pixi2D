using Pixi2D.Markup;
using QuickJsNet;

namespace Pixi2D.Scripting.QuickJs;

/// <summary>
/// 把 <see cref="QuickJSEngine"/> 适配为 Pixi2D 的 <see cref="IScriptEngine"/>。
/// </summary>
public sealed class QuickJsScriptEngine : IScriptEngine
{
    private readonly QuickJSEngine _engine;
    private bool _disposed;

    public QuickJsScriptEngine(QuickJSEngine? engine = null)
    {
        _engine = engine ?? new QuickJSEngine();
    }

    /// <summary>暴露底层引擎，便于调用 <see cref="QuickJSEngine.SetGlobalStatic{T}"/> 等高级 API。</summary>
    public QuickJSEngine Inner => _engine;

    /// <summary>转发 QuickJS 的 OnLog（QuickJS 内部 console.log 路径）。level: 0=log/info, 1=warn, 2=error。</summary>
    public event Action<int, string>? OnLog
    {
        add { _engine.OnLog += value; }
        remove { _engine.OnLog -= value; }
    }

    public void Execute(string source, string? sourceName = null)
        => _engine.Execute(source, sourceName ?? "<eval>");

    public void SetGlobal(string name, object? value)
        => _engine.SetGlobal(name, value);

    public object? Invoke(string functionName, params object?[] args)
    {
        var nonNull = new object[args.Length];
        for (int i = 0; i < args.Length; i++) nonNull[i] = args[i]!;
        return _engine.Invoke(functionName, nonNull);
    }

    public void RegisterFunction(string name, Func<object?[], object?> implementation)
        => _engine.RegisterFunction(name, implementation, argCount: 0);

    public void Pump() => _engine.PumpEventLoop();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
    }
}
