using Pixi2D.Controls;
using Pixi2D.Core;
using Pixi2D.Extensions;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace Pixi2D.Components;

/// <summary>
/// Toast 弹出位置。
/// </summary>
public enum ToastPosition
{
    TopRight,
    TopCenter,
    TopLeft,
    BottomRight,
    BottomCenter,
    BottomLeft,
}

/// <summary>
/// 内置 Toast 风格。
/// </summary>
public enum ToastStyle
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// 轻量瞬时通知。<br/>
/// 用法：<c>Toast.Info(stage, "已保存");</c><br/>
/// 高级用法：<see cref="Show(Stage, string, TimeSpan, BrushStyle, BrushStyle, BrushStyle?, FancyText.Factory?)"/>。
/// 内部使用 <see cref="FancyText"/> 渲染并通过 <see cref="AnimatorExtensions.Animate(DisplayObject, float, AnimatorUpdateCallback, EasingFunction, float)"/>
/// 回调重载实现 AOT 安全的淡入/淡出。
/// </summary>
public sealed class Toast : Container
{
    /// <summary>同时存活的 Toast 上限；超过时最早的会被立即关闭。</summary>
    public static int MaxCount { get; set; } = 5;

    /// <summary>所有 Toast 共享的弹出位置。</summary>
    public new static ToastPosition Position { get; set; } = ToastPosition.TopRight;

    /// <summary>距离 Stage 边缘的内边距。</summary>
    public static float Margin { get; set; } = 16f;

    /// <summary>相邻 Toast 之间的垂直间距。</summary>
    public static float Spacing { get; set; } = 8f;

    /// <summary>预设便捷方法的默认存留时间。</summary>
    public static TimeSpan DefaultDuration { get; set; } = TimeSpan.FromSeconds(3);

    private const float FadeInDuration = 0.18f;
    private const float FadeOutDuration = 0.25f;
    private const float ReflowDuration = 0.18f;

    private readonly FancyText _content;
    private readonly TaskCompletionSource _closedTcs = new();
    private bool _closing;

    /// <summary>当 Toast 已淡出并从 Stage 移除时完成。</summary>
    public Task Closed => _closedTcs.Task;

    /// <summary>所属宿主（每个 Stage 一个）。</summary>
    internal ToastHost? HostRef { get; set; }

    private Toast(FancyText content)
    {
        _content = content;
        AddChild(content);
        Width = content.Width;
        Height = content.Height;
        Alpha = 0f;
    }

    /// <summary>
    /// 显示一个 Toast。
    /// </summary>
    public static Toast Show(Stage stage, string message, TimeSpan duration,
                             BrushStyle textStyle, BrushStyle bgStyle,
                             BrushStyle? borderStyle = null,
                             FancyText.Factory? factory = null)
    {
        ArgumentNullException.ThrowIfNull(stage);
        message ??= string.Empty;

        var fac = factory ?? new FancyText.Factory
        {
            FontSize = 14f,
            PaddingHorizontal = 12f,
            PaddingVertical = 8f,
        };

        var ft = fac.Create(message);
        ft.WordWrap = true;
        ft.MaxTextWidth = Math.Min(Math.Max(stage.Width - Margin * 2 - 16f, 120f), 360f);
        ft.BorderRadius = 6f;
        ft.TextStyle = textStyle;
        ft.BackgroundStyle = bgStyle;
        if (borderStyle.HasValue)
        {
            ft.BorderStyle = borderStyle.Value;
            ft.BorderWidth = 1f;
        }

        var toast = new Toast(ft);
        var host = ToastHost.GetOrCreate(stage);
        host.Add(toast);

        // 启动生命周期：淡入 → 等待 → 淡出 → 移除。
        _ = toast.RunAsync(duration);
        return toast;
    }

    /// <summary>
    /// 提前关闭 Toast。多次调用幂等。
    /// </summary>
    public void Close()
    {
        if (_closing) return;
        _closing = true;
        _ = FadeOutAndRemoveAsync();
    }

    private async Task RunAsync(TimeSpan duration)
    {
        try
        {
            await FadeAsync(0f, 1f, FadeInDuration).ConfigureAwait(true);
            if (_closing) return;
            var ms = (int)Math.Max(0, duration.TotalMilliseconds);
            if (ms > 0) await Task.Delay(ms).ConfigureAwait(true);
            if (_closing) return;
            _closing = true;
            await FadeOutAndRemoveAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // 兜底：异常也要保证从舞台移除并完成 Closed。
            System.Diagnostics.Debug.WriteLine($"[Toast] RunAsync failed: {ex}");
            ForceRemove();
        }
    }

    private async Task FadeOutAndRemoveAsync()
    {
        try
        {
            await FadeAsync(Alpha, 0f, FadeOutDuration).ConfigureAwait(true);
        }
        catch { /* ignore */ }
        ForceRemove();
    }

    private void ForceRemove()
    {
        try
        {
            HostRef?.Remove(this);
        }
        finally
        {
            _closedTcs.TrySetResult();
        }
    }

    /// <summary>
    /// AOT 安全的 Alpha 动画：使用 <see cref="AnimatorExtensions.Animate(DisplayObject,float,AnimatorUpdateCallback,EasingFunction,float)"/>
    /// 回调重载，禁用匿名对象属性反射。
    /// </summary>
    private Task FadeAsync(float from, float to, float seconds)
    {
        Alpha = from;
        var animator = this.Animate(seconds, (sender, factor) =>
        {
            // 线性插值；外层可换其他 Easing 但这里手动算保持显式。
            sender.Target.Alpha = from + (to - from) * factor;
        }, EasingFunction.QuadraticEaseOut);
        return animator.Task;
    }

    /// <summary>由 ToastHost 调用：以 AOT 安全方式平滑滑动到目标 Y。</summary>
    internal void AnimateToY(float targetY)
    {
        var fromY = Y;
        if (Math.Abs(targetY - fromY) < 0.5f)
        {
            Y = targetY;
            return;
        }
        this.Animate(ReflowDuration, (sender, factor) =>
        {
            sender.Target.Y = fromY + (targetY - fromY) * factor;
        }, EasingFunction.QuadraticEaseOut);
    }

    public override bool HitTest(PointF localPoint) => false; // 不阻断业务交互

    #region 预设便捷方法

    public static Toast Info(Stage stage, string message, TimeSpan? duration = null)
        => ShowPreset(stage, message, ToastStyle.Info, duration);

    public static Toast Success(Stage stage, string message, TimeSpan? duration = null)
        => ShowPreset(stage, message, ToastStyle.Success, duration);

    public static Toast Warning(Stage stage, string message, TimeSpan? duration = null)
        => ShowPreset(stage, message, ToastStyle.Warning, duration);

    public static Toast Error(Stage stage, string message, TimeSpan? duration = null)
        => ShowPreset(stage, message, ToastStyle.Error, duration);

    private static Toast ShowPreset(Stage stage, string message, ToastStyle style, TimeSpan? duration)
    {
        var (text, bg, prefix) = GetPreset(style);
        var msg = string.IsNullOrEmpty(prefix) ? (message ?? string.Empty) : $"{prefix} {message}";
        return Show(stage, msg, duration ?? DefaultDuration, text, bg);
    }

    private static (BrushStyle text, BrushStyle bg, string prefix) GetPreset(ToastStyle style) => style switch
    {
        ToastStyle.Success => (
            new BrushStyle(Color.White),
            new BrushStyle(Color.FromArgb(67, 160, 71)),
            "✓"),
        ToastStyle.Warning => (
            new BrushStyle(Color.FromArgb(33, 33, 33)),
            new BrushStyle(Color.FromArgb(249, 168, 37)),
            "⚠"),
        ToastStyle.Error => (
            new BrushStyle(Color.White),
            new BrushStyle(Color.FromArgb(211, 47, 47)),
            "✕"),
        _ => (
            new BrushStyle(Color.White),
            new BrushStyle(Color.FromArgb(50, 50, 50)),
            "ℹ"),
    };

    #endregion
}

/// <summary>
/// 每个 Stage 持有一个 ToastHost，负责堆叠/重排所有 Toast。
/// 通过 <see cref="ConditionalWeakTable{TKey,TValue}"/> 与 Stage 关联，避免泄漏。
/// </summary>
internal sealed class ToastHost : Container
{
    private static readonly ConditionalWeakTable<Stage, ToastHost> s_table = new();

    private readonly Stage _stage;
    private readonly List<Toast> _toasts = new();

    private ToastHost(Stage stage)
    {
        _stage = stage;
        _stage.OnResize += OnStageResize;
    }

    public static ToastHost GetOrCreate(Stage stage)
    {
        if (s_table.TryGetValue(stage, out var existing))
        {
            // 若被外部移除，重新挂上。
            if (existing.Parent is null) stage.AddChild(existing);
            return existing;
        }
        var host = new ToastHost(stage);
        s_table.Add(stage, host);
        stage.AddChild(host);
        return host;
    }

    private void OnStageResize(Stage stage, float width, float height) => Reflow(animate: false);

    public void Add(Toast toast)
    {
        // FIFO 挤出：先把超额的最早 toast 关闭。
        while (_toasts.Count >= Math.Max(1, Toast.MaxCount))
        {
            var oldest = _toasts[0];
            // Close 是异步的，但会立即从列表移除以释放位置。
            _toasts.RemoveAt(0);
            oldest.Close();
        }

        toast.HostRef = this;
        _toasts.Add(toast);
        AddChild(toast);
        Reflow(animate: true);
    }

    public void Remove(Toast toast)
    {
        if (_toasts.Remove(toast))
        {
            if (toast.Parent is not null) RemoveChild(toast);
            Reflow(animate: true);
        }
    }

    private void Reflow(bool animate)
    {
        var stageW = _stage.Width;
        var stageH = _stage.Height;
        var pos = Toast.Position;
        var margin = Toast.Margin;
        var spacing = Toast.Spacing;

        bool fromTop = pos is ToastPosition.TopLeft or ToastPosition.TopCenter or ToastPosition.TopRight;

        // 自顶向下/自底向上排布
        float cursor = fromTop ? margin : (stageH - margin);

        if (fromTop)
        {
            for (int i = 0; i < _toasts.Count; i++)
            {
                var t = _toasts[i];
                var x = ComputeX(pos, stageW, t.Width, margin);
                t.X = x;
                if (animate) t.AnimateToY(cursor); else t.Y = cursor;
                cursor += t.Height + spacing;
            }
        }
        else
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                var x = ComputeX(pos, stageW, t.Width, margin);
                t.X = x;
                cursor -= t.Height;
                if (animate) t.AnimateToY(cursor); else t.Y = cursor;
                cursor -= spacing;
            }
        }
    }

    private static float ComputeX(ToastPosition pos, float stageW, float w, float margin) => pos switch
    {
        ToastPosition.TopLeft or ToastPosition.BottomLeft => margin,
        ToastPosition.TopCenter or ToastPosition.BottomCenter => MathF.Max(0, (stageW - w) / 2f),
        _ => MathF.Max(0, stageW - w - margin),
    };

    public override bool HitTest(PointF localPoint) => false;
}
