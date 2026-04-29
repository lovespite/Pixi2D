using Pixi2D.Core;

namespace Pixi2D.Scripting;

/// <summary>
/// 控件代理标记接口。<br />
/// 由具体脚本适配器（如 Pixi2D.Scripting.QuickJs 中的 [JSExport] 代理）实现。
/// </summary>
public interface IControlProxy
{
    /// <summary>被代理的真实 Pixi2D 控件。</summary>
    DisplayObject Wrapped { get; }
}

/// <summary>
/// 控件代理工厂抽象。<br />
/// 由具体脚本适配器实现：根据 DisplayObject 实际类型返回对应的 [JSExport] 代理实例。
/// </summary>
public interface IProxyFactory
{
    /// <summary>
    /// 为指定控件创建脚本可见的代理对象。返回 <c>null</c> 表示该类型不支持代理（将被跳过）。
    /// </summary>
    object? Create(DisplayObject control);
}

/// <summary>
/// 标记需要 JS 端薄包装（automatic JSON.stringify）的代理。<br />
/// <see cref="ScriptBootstrap"/> 在 <see cref="IScriptEngine.SetGlobal"/> 之后会按 <see cref="ShimMethods"/>
/// 注入 <c>obj.method = (function(o){ return function(){ var a=[].slice.call(arguments); for (var i of indices) a[i]=JSON.stringify(a[i]); return o(...a); }; })(obj.method.bind(obj))</c>
/// 形式的 monkey patch，让脚本侧可直接 <c>tbl.setData([[...]])</c>。
/// </summary>
public interface IJsonShimProxy
{
    /// <summary>需要包装的 (JS 方法名, 需 JSON 化的参数索引集合) 列表。</summary>
    IReadOnlyList<(string Method, int[] JsonArgIndices)> ShimMethods { get; }
}
