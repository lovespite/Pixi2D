namespace Pixi2D.Components;

public static class InfiniteAnimatorExtensions
{
    /// <summary>
    /// 创建并开始一个无限动画器。
    /// </summary>
    public static InfiniteAnimator InfiniteAnimate(this DisplayObject target, object properties, float duration, bool restoreOnStop = true, EasingFunction easing = EasingFunction.Linear, InfiniteLoopMode loopMode = InfiniteLoopMode.Restart)
    {
        return new InfiniteAnimator(target, properties, duration, easing, loopMode, restoreOnStop);
    }

    public static InfiniteAnimator InfiniteAnimate(this DisplayObject target, float duration, InfiniteAnimatorUpdateCallback cb, bool restoreOnStop = true, EasingFunction easing = EasingFunction.Linear, InfiniteLoopMode loopMode = InfiniteLoopMode.Restart)
    {
        var animator = new InfiniteAnimator(target, null, duration, easing, loopMode, restoreOnStop);
        animator.Animating += cb;
        return animator;
    }

    /// <summary>
    /// 创建并播放一个无限循环的动画。
    /// </summary>
    /// <param name="target">目标对象。</param>
    /// <param name="properties">目标属性集合，例如 new { Rotation = 6.28f }。</param>
    /// <param name="duration">单次循环持续时间。</param>
    /// <param name="loopMode">循环模式 (Restart 或 PingPong)。</param>
    /// <param name="easing">缓动函数。</param>
    /// <returns>InfiniteAnimator 实例，可用于手动停止。</returns>
    public static InfiniteAnimator AnimateLoop(this DisplayObject target, object properties, float duration, bool restoreOnStop = true, InfiniteLoopMode loopMode = InfiniteLoopMode.Restart, EasingFunction easing = EasingFunction.Linear)
    {
        return new InfiniteAnimator(target, properties, duration, easing, loopMode, restoreOnStop);
    }

    /// <summary>
    /// 无限旋转 (增量旋转)。
    /// 注意：如果只想让物体一直转下去，通常使用 Restart 模式，并将目标旋转角度设为 当前角度 + 2PI (或 360度)。
    /// 但由于 InfiniteAnimator 是基于 StartValue 和 EndValue 插值的，
    /// 所以要在视觉上实现无缝旋转，StartValue (0) 和 EndValue (360) 的视觉状态必须一致。
    /// </summary>
    public static InfiniteAnimator RotateLoop(this DisplayObject target, float targetRotation, float duration, bool restoreOnStop = true, EasingFunction easing = EasingFunction.Linear)
    {
        return new InfiniteAnimator(target, new { Rotation = targetRotation }, duration, easing, InfiniteLoopMode.Restart, restoreOnStop);
    }

    /// <summary>
    /// 像呼吸一样缩放 (PingPong 模式)。
    /// </summary>
    public static InfiniteAnimator ScaleBreath(this DisplayObject target, float targetScale, float duration, bool restoreOnStop = true, EasingFunction easing = EasingFunction.CircleEaseInOut)
    {
        return new InfiniteAnimator(target, new { ScaleX = targetScale, ScaleY = targetScale }, duration, easing, InfiniteLoopMode.PingPong, restoreOnStop);
    }

    /// <summary>
    /// 像呼吸一样闪烁 (PingPong 模式)。
    /// </summary>
    public static InfiniteAnimator AlphaBreath(this DisplayObject target, float minAlpha, float duration, bool restoreOnStop = true, EasingFunction easing = EasingFunction.Linear)
    {
        // 注意：InfiniteAnimator 构造时会读取当前 Alpha 作为 StartValue。
        // 如果想要在 minAlpha 和 当前Alpha 之间闪烁，确保调用时 Alpha 是最大值。
        return new InfiniteAnimator(target, new { Alpha = minAlpha }, duration, easing, InfiniteLoopMode.PingPong, restoreOnStop);
    }
}