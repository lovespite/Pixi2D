using Pixi2D.Components;
using Pixi2D.Controls;
using Pixi2D.Core;

namespace Pixi2D.Scripting.QuickJs;

/// <summary>
/// 静态分派的代理工厂；无任何运行时反射。<br />
/// 不在表中的类型回退到通用 <see cref="DisplayObjectProxy"/>。
/// </summary>
public sealed class QuickJsProxyFactory : IProxyFactory
{
    public object? Create(DisplayObject control) => control switch
    {
        Button b => new ButtonProxy(b),
        Switch s => new SwitchProxy(s),
        FancyText ft => new FancyTextProxy(ft),
        TextBox tb => new TextBoxProxy(tb),
        Number n => new NumberProxy(n),
        ProgressBar pb => new ProgressBarProxy(pb),
        Modal m => new ModalProxy(m),
        Panel p => new PanelProxy(p),
        Container c => new ContainerProxy(c),
        _ => new DisplayObjectProxy(control),
    };
}
