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
