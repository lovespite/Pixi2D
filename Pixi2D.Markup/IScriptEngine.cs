using Pixi2D.Core;

namespace Pixi2D.Markup;

/// <summary>
/// 脚本引擎抽象。<br />
/// Pixi2D.Markup 不强制绑定具体 JS 引擎；用户可注入 Jint / ClearScript / 其他实现。
/// </summary>
public interface IScriptEngine : IDisposable
{
    /// <summary>
    /// 执行脚本源码。
    /// </summary>
    void Execute(string source, string? sourceName = null);

    /// <summary>
    /// 将一个 .NET 对象/值绑定到脚本全局命名空间。
    /// </summary>
    void SetGlobal(string name, object? value);

    /// <summary>
    /// 调用脚本中已定义的全局函数。
    /// </summary>
    object? Invoke(string functionName, params object?[] args);

    /// <summary>
    /// 注册一个全局可调用的 .NET 委托（基础类型参数/返回值），供脚本侧调用。
    /// </summary>
    void RegisterFunction(string name, Func<object?[], object?> implementation);

    /// <summary>
    /// 推动一次脚本引擎的内置事件循环（处理 setTimeout / setInterval / 微任务等）。
    /// 默认空实现；不支持事件循环的引擎可忽略。宿主可在每帧渲染前调用以保证定时器滴答。
    /// 必须在 JS 线程调用。
    /// </summary>
    void Pump() { }
}

/// <summary>
/// 脚本宿主：维护脚本引擎、命名控件表 (id → DisplayObject) 与挂起的事件绑定。
/// </summary>
public sealed class ScriptHost
{
    public IScriptEngine? Engine { get; set; }

    /// <summary>
    /// 通过 id/name 索引到的命名控件。
    /// </summary>
    public Dictionary<string, DisplayObject> NamedObjects { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 挂起的事件绑定：(目标对象, 事件名, 处理函数名)。
    /// 在脚本引擎装载脚本之后由用户/loader 应用。
    /// </summary>
    public List<PendingHandler> PendingHandlers { get; } = new();

    public readonly record struct PendingHandler(DisplayObject Target, string EventName, string HandlerName);
}

/// <summary>
/// 永远不执行任何脚本的桩实现。可用于纯 UI 描述场景。
/// </summary>
public sealed class NullScriptEngine : IScriptEngine
{
    public static readonly NullScriptEngine Instance = new();
    private NullScriptEngine() { }

    public void Execute(string source, string? sourceName = null) { }
    public void SetGlobal(string name, object? value) { }
    public object? Invoke(string functionName, params object?[] args) => null;
    public void RegisterFunction(string name, Func<object?[], object?> implementation) { }
    public void Dispose() { }
}
