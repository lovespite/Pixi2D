using SharpDX.Mathematics.Interop;

namespace Pixi2D.Core;

/// <summary>
/// 全局 UI 环境上下文。<br />
/// 供控件无参构造、XML(DSL) 反序列化器读取共享资源
/// (DirectWrite Factory、文本工厂、资源加载器等)。
/// <para>
/// 用法：
/// <code>
/// // 应用启动时配置一次：
/// UIContext.Current.DefaultFontFamily = "Microsoft YaHei";
/// UIContext.Current.DefaultFontSize = 14f;
/// UIContext.Current.Assets = new MyAssetLoader();
/// </code>
/// </para>
/// <para>
/// 支持作用域栈（<see cref="Push"/>/<see cref="Pop"/>），便于预览器或单元测试切换上下文。
/// </para>
/// </summary>
public sealed class UIContext
{
    private static readonly Stack<UIContext> s_stack = new();
    private static UIContext s_current = new();

    /// <summary>
    /// 当前生效的上下文。永远非 null。
    /// </summary>
    public static UIContext Current
    {
        get => s_current;
        set => s_current = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 将当前上下文压栈，并把 <paramref name="ctx"/> 设为新的 Current。
    /// 通过返回的 <see cref="Scope"/> 在 dispose 时自动恢复。
    /// </summary>
    public static Scope Push(UIContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        s_stack.Push(s_current);
        s_current = ctx;
        return new Scope();
    }

    /// <summary>
    /// 弹出上一层上下文。若栈空则保持当前。
    /// </summary>
    public static void Pop()
    {
        if (s_stack.Count > 0) s_current = s_stack.Pop();
    }

    /// <summary>
    /// 由 <see cref="Push(UIContext)"/> 返回，dispose 时自动恢复上一层上下文。
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        public void Dispose() => Pop();
    }

    // ---------------- 实例字段 ----------------

    /// <summary>
    /// 默认 DirectWrite Factory。若为 null，<see cref="DefaultTextFactory"/> 将
    /// 退回到 <see cref="Text.Factory"/> 内置的共享 Shared 实例。
    /// </summary>
    public SharpDX.DirectWrite.Factory? DWriteFactory { get; set; }

    private Text.Factory? _defaultTextFactory;

    /// <summary>
    /// 默认文本工厂。若未显式设置，按需自动构造一个并应用本上下文的默认字体属性。
    /// </summary>
    public Text.Factory DefaultTextFactory
    {
        get
        {
            if (_defaultTextFactory is null)
            {
                _defaultTextFactory = DWriteFactory is null
                    ? new Text.Factory()
                    : new Text.Factory(DWriteFactory);
                ApplyDefaultsTo(_defaultTextFactory);
            }
            return _defaultTextFactory;
        }
        set => _defaultTextFactory = value;
    }

    /// <summary>
    /// 资源加载器。默认 <see cref="NullAssetLoader.Instance"/>。
    /// </summary>
    public IAssetLoader Assets { get; set; } = NullAssetLoader.Instance;

    private WeakReference<Stage>? _stageRef;

    /// <summary>
    /// 应用默认 Stage 的弱引用（可选，便于 Modal/MessageBox 等组件无参构造定位）。
    /// </summary>
    public Stage? DefaultStage
    {
        get => (_stageRef is not null && _stageRef.TryGetTarget(out var s)) ? s : null;
        set => _stageRef = value is null ? null : new WeakReference<Stage>(value);
    }

    public string DefaultFontFamily { get; set; } = "Arial";
    public float DefaultFontSize { get; set; } = 14f;
    public SharpDX.DirectWrite.FontWeight DefaultFontWeight { get; set; } = SharpDX.DirectWrite.FontWeight.Regular;
    public SharpDX.DirectWrite.FontStyle DefaultFontStyle { get; set; } = SharpDX.DirectWrite.FontStyle.Normal;

    /// <summary>
    /// 默认前景色（白色）。
    /// </summary>
    public RawColor4 DefaultFontColor { get; set; } = new RawColor4(1f, 1f, 1f, 1f);

    private void ApplyDefaultsTo(Text.Factory f)
    {
        f.FontFamily = DefaultFontFamily;
        f.FontSize = DefaultFontSize;
        f.FontStyle = DefaultFontStyle;
        f.FontWeight = DefaultFontWeight;
        f.FillColor = System.Drawing.Color.FromArgb(
            (int)(DefaultFontColor.A * 255),
            (int)(DefaultFontColor.R * 255),
            (int)(DefaultFontColor.G * 255),
            (int)(DefaultFontColor.B * 255));
    }
}
