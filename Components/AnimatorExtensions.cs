namespace Pixi2D.Components;

public static class AnimatorExtensions
{
    /// <summary>
    /// 动画最终透明属性对象。
    /// </summary>
    public static readonly object AnimatorPropTransparent = new { Alpha = 0f };
    /// <summary>
    /// 动画最终不透明属性对象。 
    /// </summary>
    public static readonly object AnimatorPropOpaque = new { Alpha = 1f };

    /// <summary>
    /// 创建并播放一个通用的属性动画。
    /// </summary>
    /// <param name="target">目标对象。</param>
    /// <param name="properties">目标属性集合 (匿名对象)，例如 new { X = 100, Alpha = 0 }。</param>
    /// <param name="duration">持续时间 (秒)。</param>
    /// <param name="easing">缓动函数。</param>
    /// <param name="delay">延迟时间 (秒)。</param>
    /// <returns>创建的 Animator 实例，可用于控制或等待。</returns>
    /// <remarks>
    /// 在启用裁剪、AOT发布的项目中，请使用回调的重载版本。<br />
    /// 参见<see cref="Animate(DisplayObject, float, AnimatorUpdateCallback, EasingFunction, float)"/>
    /// </remarks>
    public static Animator Animate(this DisplayObject target, object properties, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, properties, duration, easing, delay);
    }

    /// <summary>
    /// 创建并播放一个自定义更新回调的动画。<br />可用于实现更复杂的动画效果。
    /// </summary>
    /// <param name="target">目标对象。</param>
    /// <param name="duration">持续时间 (秒)。</param>
    /// <param name="cb">
    /// 更新回调函数，接收当前进度 (0 到 1 之间的浮点数) 作为参数。
    /// </param>
    /// <param name="easing">缓动函数。</param>
    /// <param name="delay">延迟时间 (秒)。</param>
    /// <returns>创建的 Animator 实例，可用于控制或等待。</returns>
    public static Animator Animate(this DisplayObject target, float duration, AnimatorUpdateCallback cb, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        var a = new Animator(target, null, duration, easing, delay);
        a.Animating += cb;
        return a;
    }

    #region Move Animations

    public static Animator MoveTo(this DisplayObject target, float x, float y, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { X = x, Y = y }, duration, easing, delay);
    }

    public static Animator MoveXTo(this DisplayObject target, float x, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { X = x }, duration, easing, delay);
    }

    public static Animator MoveYTo(this DisplayObject target, float y, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { Y = y }, duration, easing, delay);
    }

    public static Animator LinearMoveTo(this DisplayObject target, float x, float y, float duration, float delay = 0f)
    {
        return new Animator(target, new { X = x, Y = y }, duration, EasingFunction.Linear, delay);
    }

    public static Animator CircleMoveTo(this DisplayObject target, float x, float y, float duration, float delay = 0f)
    {
        return new Animator(target, new { X = x, Y = y }, duration, EasingFunction.CircleEaseInOut, delay);
    }

    public static Animator CubicMoveTo(this DisplayObject target, float x, float y, float duration, float delay = 0f)
    {
        return new Animator(target, new { X = x, Y = y }, duration, EasingFunction.CubicEaseInOut, delay);
    }

    public static Animator Move(this DisplayObject target, float deltaX, float deltaY, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { X = target.X + deltaX, Y = target.Y + deltaY }, duration, easing, delay);
    }

    public static Animator LinearMove(this DisplayObject target, float deltaX, float deltaY, float duration, float delay = 0f)
    {
        return new Animator(target, new { X = target.X + deltaX, Y = target.Y + deltaY }, duration, EasingFunction.Linear, delay);
    }

    public static Animator CircleMove(this DisplayObject target, float deltaX, float deltaY, float duration, float delay = 0f)
    {
        return new Animator(target, new { X = target.X + deltaX, Y = target.Y + deltaY }, duration, EasingFunction.CircleEaseInOut, delay);
    }

    public static Animator CubicMove(this DisplayObject target, float deltaX, float deltaY, float duration, float delay = 0f)
    {
        return new Animator(target, new { X = target.X + deltaX, Y = target.Y + deltaY }, duration, EasingFunction.CubicEaseInOut, delay);
    }

    #endregion

    #region Fade Animations
    /// <summary>
    /// 快速创建透明度动画 (修改 Alpha 属性)。
    /// </summary>
    public static Animator LinearFadeTo(this DisplayObject target, float alpha, float duration, float delay = 0f)
    {
        return new Animator(target, new { Alpha = alpha }, duration, EasingFunction.Linear, delay);
    }

    public static Animator LinearFadeIn(this DisplayObject target, float duration, float delay = 0f)
    {
        return new Animator(target, AnimatorPropOpaque, duration, EasingFunction.Linear, delay);
    }

    public static Animator LinearFadeOut(this DisplayObject target, float duration, float delay = 0f)
    {
        return new Animator(target, AnimatorPropTransparent, duration, EasingFunction.Linear, delay);
    }

    public static Animator CircleFadeTo(this DisplayObject target, float alpha, float duration, float delay = 0f)
    {
        return new Animator(target, new { Alpha = alpha }, duration, EasingFunction.CircleEaseInOut, delay);
    }

    public static Animator CircleFadeIn(this DisplayObject target, float duration, float delay = 0f)
    {
        return new Animator(target, AnimatorPropOpaque, duration, EasingFunction.CircleEaseIn, delay);
    }

    public static Animator CircleFadeOut(this DisplayObject target, float duration, float delay = 0f)
    {
        return new Animator(target, AnimatorPropTransparent, duration, EasingFunction.CircleEaseOut, delay);
    }

    public static Animator CubicFadeTo(this DisplayObject target, float alpha, float duration, float delay = 0f)
    {
        return new Animator(target, new { Alpha = alpha }, duration, EasingFunction.CubicEaseInOut, delay);
    }

    public static Animator CubicFadeIn(this DisplayObject target, float duration, float delay = 0f)
    {
        return new Animator(target, AnimatorPropOpaque, duration, EasingFunction.CubicEaseIn, delay);
    }

    public static Animator CubicFadeOut(this DisplayObject target, float duration, float delay = 0f)
    {
        return new Animator(target, AnimatorPropTransparent, duration, EasingFunction.CubicEaseOut, delay);
    }

    #endregion

    #region Scale Animations

    /// <summary>
    /// 快速创建缩放动画 (分别修改 ScaleX 和 ScaleY 属性)。
    /// </summary>
    public static Animator ScaleTo(this DisplayObject target, float scaleX, float scaleY, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { ScaleX = scaleX, ScaleY = scaleY }, duration, easing, delay);
    }

    public static Animator ScaleXTo(this DisplayObject target, float scaleX, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { ScaleX = scaleX }, duration, easing, delay);
    }

    public static Animator ScaleYTo(this DisplayObject target, float scaleY, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { ScaleY = scaleY }, duration, easing, delay);
    }

    /// <summary>
    /// 快速创建统一缩放动画 (同时修改 ScaleX 和 ScaleY 属性)。
    /// </summary>
    public static Animator ScaleTo(this DisplayObject target, float scale, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { ScaleX = scale, ScaleY = scale }, duration, easing, delay);
    }

    #endregion

    #region Rotate Animations

    /// <summary>
    /// 快速创建旋转动画 (修改 Rotation 属性)。
    /// </summary>
    public static Animator RotateTo(this DisplayObject target, float rotation, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { Rotation = rotation }, duration, easing, delay);
    }

    /// <summary>
    /// 快速创建旋转动画 (修改 Rotation 属性)。
    /// </summary>
    public static Animator Rotate(this DisplayObject target, float rotationDelta, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        return new Animator(target, new { Rotation = target.Rotation + rotationDelta }, duration, easing, delay);
    }

    #endregion
}