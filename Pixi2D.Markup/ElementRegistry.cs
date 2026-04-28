using System.Collections.Concurrent;
using Pixi2D;
using Pixi2D.Components;
using Pixi2D.Components.Utils;
using Pixi2D.Controls;
using Pixi2D.Core;

namespace Pixi2D.Markup;

/// <summary>
/// XML(DSL) 元素名 ↔ 控件类型映射表。<br />
/// 内置 Pixi2D 全部公共控件/组件，并提供 <see cref="Register"/> 给用户扩展自定义元素。
/// </summary>
public static class ElementRegistry
{
    private static readonly ConcurrentDictionary<string, Type> s_typeMap = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Func<DisplayObject>> s_factoryMap = new(StringComparer.Ordinal);

    static ElementRegistry()
    {
        // Core (Sprite 需要 D2DBitmap，请按需 RegisterFactory("sprite", ...))
        Register("text", typeof(Text));
        Register("graphics", typeof(Graphics));
        Register("container", typeof(Container));

        // Controls
        Register("panel", typeof(Panel));
        Register("button", typeof(Button));
        Register("fancy-text", typeof(FancyText));
        Register("text-box", typeof(TextBox));
        Register("combo-box", typeof(ComboBox));
        Register("switch", typeof(Switch));
        Register("number", typeof(Number));
        Register("progress-bar", typeof(ProgressBar));
        Register("list-item", typeof(ListItem));
#pragma warning disable CS0618
        Register("list", typeof(List));
        Register("scrollable-list", typeof(ScrollableList));
#pragma warning restore CS0618
        // VirtualScrollList<T> 是泛型，需用户为具体 T 注册，例如：
        //   ElementRegistry.Register("virtual-scroll-list-string", typeof(VirtualScrollList<string>));
        Register("tree-view", typeof(TreeView));
        Register("tree-node", typeof(TreeNode));
        Register("table", typeof(Table));
        Register("table-cell", typeof(TableCell));
        Register("graphics-spin-loading", typeof(GraphicsSpinLoading));

        // Components
        Register("modal", typeof(Modal));
        Register("flow-layout", typeof(FlowLayout));
        Register("auto-flow-layout", typeof(AutoFlowLayout));
        Register("soft-keyboard", typeof(SoftKeyboard));

        // 资源依赖型控件 (icon-label / fancy-button / spin-loading / message-box):
        // 因构造时需要 D2DBitmap/Sprite 等资源，未注册默认工厂。
        // 用户可通过 ElementRegistry.RegisterFactory("icon-label", () => ...) 注入自定义实现。
    }

    /// <summary>
    /// 按元素名注册一个控件类型（要求类型有公共无参构造）。
    /// </summary>
    public static void Register(string elementName, Type type)
    {
        ArgumentException.ThrowIfNullOrEmpty(elementName);
        ArgumentNullException.ThrowIfNull(type);
        if (!typeof(DisplayObject).IsAssignableFrom(type))
            throw new ArgumentException($"类型 {type.FullName} 不是 DisplayObject 的子类。", nameof(type));
        s_typeMap[elementName] = type;
    }

    /// <summary>
    /// 按元素名注册一个工厂函数（用于无法用无参构造创建的资源依赖型控件）。
    /// </summary>
    public static void RegisterFactory(string elementName, Func<DisplayObject> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(elementName);
        ArgumentNullException.ThrowIfNull(factory);
        s_factoryMap[elementName] = factory;
    }

    /// <summary>反查元素名是否注册了工厂函数。</summary>
    public static bool HasFactory(string elementName)
        => s_factoryMap.ContainsKey(elementName);

    /// <summary>
    /// 反查元素名对应的类型。未注册时返回 null。
    /// </summary>
    public static Type? Resolve(string elementName)
        => s_typeMap.TryGetValue(elementName, out var t) ? t : null;

    /// <summary>
    /// 创建对应元素的控件实例。优先使用工厂；否则用无参构造。
    /// </summary>
    public static DisplayObject Create(string elementName)
    {
        if (s_factoryMap.TryGetValue(elementName, out var factory))
            return factory();
        if (s_typeMap.TryGetValue(elementName, out var type))
            return (DisplayObject)Activator.CreateInstance(type)!;
        throw new InvalidOperationException($"未知的 PXML 元素 <{elementName}>。请通过 ElementRegistry.Register 注册。");
    }

    /// <summary>
    /// 枚举所有已注册元素名。
    /// </summary>
    public static IEnumerable<string> RegisteredElements
        => s_typeMap.Keys.Concat(s_factoryMap.Keys).Distinct(StringComparer.Ordinal);
}
