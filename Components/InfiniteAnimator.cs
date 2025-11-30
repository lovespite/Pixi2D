using System.Collections.Concurrent;
using System.Linq.Expressions;
using Pixi2D.Core;

namespace Pixi2D.Components;

public delegate void InfiniteAnimatorUpdateCallback(InfiniteAnimator sender, float factor);

/// <summary>
/// 无限动画的循环模式。
/// </summary>
public enum InfiniteLoopMode
{
    /// <summary>
    /// 重新开始。播放到结尾后立即回到起点重新播放 (0 -> 1, 0 -> 1)。
    /// </summary>
    Restart,
    /// <summary>
    /// 往返播放。播放到结尾后倒序播放回起点 (0 -> 1 -> 0)。
    /// </summary>
    PingPong
}

/// <summary>
/// 无限动画器。
/// 用于对 DisplayObject 的属性进行无限循环的缓动动画。
/// 直到调用 Stop() 或目标对象被销毁。
/// </summary>
public class InfiniteAnimator
{
    // --- 静态缓存 (复用反射/表达式树逻辑以提高性能) ---
    private static readonly ConcurrentDictionary<(Type, string), (Func<object, float> Getter, Action<object, float> Setter)> _accessorCache = new();

    private readonly DisplayObject _target;
    private readonly List<AnimationTrack> _tracks = [];
    private readonly float _duration;
    private readonly EasingFunction _easing;
    private readonly InfiniteLoopMode _loopMode;

    private float _elapsedTime;
    private bool _isPlaying;

    /// <summary>
    /// 当动画停止时触发。
    /// </summary>
    public event Action? OnStopped;

    /// <summary>
    /// 动画更新回调。
    /// </summary>
    public event InfiniteAnimatorUpdateCallback? Animating;

    /// <summary>
    /// 指示 Stop 被调用时，是否恢复至最初状态。
    /// </summary>
    public bool RestoreOnStop { get; set; }

    /// <summary>
    /// 内部结构，用于存储单条属性的动画信息
    /// </summary>
    private readonly struct AnimationTrack(Action<object, float> setter, float startValue, float endValue)
    {
        public readonly Action<object, float> Setter = setter;
        public readonly float StartValue = startValue;
        public readonly float EndValue = endValue;
    }

    /// <summary>
    /// 创建一个新的无限动画器并自动开始播放。
    /// </summary>
    /// <param name="target">动画目标。</param>
    /// <param name="properties">目标属性集合 (匿名对象)。</param>
    /// <param name="duration">单次循环的持续时间。</param>
    /// <param name="easing">缓动函数。</param>
    /// <param name="loopMode">循环模式 (Restart 或 PingPong)。</param>
    /// <remarks>
    /// 在启用裁剪、AOT发布的项目中，请使用（<see cref="Animating"/>）回调，而不是直接传入动态属性对象(properties)。<br /> 
    /// </remarks>
    public InfiniteAnimator(DisplayObject target, object? properties, float duration, EasingFunction easing = EasingFunction.Linear, InfiniteLoopMode loopMode = InfiniteLoopMode.Restart, bool restoreOnStop = true)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _duration = Math.Max(0.001f, duration); // 防止除以零
        _easing = easing;
        _loopMode = loopMode;
        _elapsedTime = 0f;

        RestoreOnStop = restoreOnStop;

        // 解析属性
        ParseProperties(properties);

        // 挂载事件
        _target.OnUpdate += Update;
        _target.OnDisposed += HandleTargetDisposed;
        _isPlaying = true;
    }

    /// <summary>
    /// 停止动画。
    /// </summary>
    public void Stop()
    {
        if (!_isPlaying) return;

        // 如果需要恢复初始状态
        if (RestoreOnStop)
        {
            foreach (var track in _tracks)
            {
                track.Setter(_target, track.StartValue);
            }
        }

        Cleanup();
        OnStopped?.Invoke();
    }

    private void HandleTargetDisposed()
    {
        Stop();
    }

    private void Cleanup()
    {
        _isPlaying = false;
        if (_target is not null)
        {
            _target.OnUpdate -= Update;
            _target.OnDisposed -= HandleTargetDisposed;
        }
    }

    private void Update(float deltaTime)
    {
        if (!_isPlaying) return;

        _elapsedTime += deltaTime;

        // 计算归一化时间 t (0.0 ~ 1.0)
        float t = 0f;

        if (_loopMode == InfiniteLoopMode.Restart)
        {
            // Restart: t 永远在 0 到 1 之间循环
            // 使用取模运算
            t = (_elapsedTime % _duration) / _duration;

            // 修正：如果恰好整除，取模结果为0，但在循环末尾我们希望它表现连贯。
            // 实际上对于Restart模式，0和1通常是突变的，所以 0 -> 1 的过程是平滑的。
        }
        else if (_loopMode == InfiniteLoopMode.PingPong)
        {
            // PingPong: 0 -> 1 -> 0
            // 周期是 2 * duration
            float doubleDuration = _duration * 2;
            float timeInCycle = _elapsedTime % doubleDuration;

            if (timeInCycle < _duration)
            {
                // 前半段: 0 -> 1
                t = timeInCycle / _duration;
            }
            else
            {
                // 后半段: 1 -> 0
                t = 1f - ((timeInCycle - _duration) / _duration);
            }
        }

        // 应用缓动
        float factor = ApplyEasing(t, _easing);

        // 更新属性
        var count = _tracks.Count;
        for (int i = 0; i < count; i++)
        {
            var track = _tracks[i];
            float current = track.StartValue + (track.EndValue - track.StartValue) * factor;
            track.Setter(_target, current);
        }

        Animating?.Invoke(this, factor);
    }

    // --- 属性解析与反射缓存 (与 Animator 逻辑一致) ---

    private void ParseProperties(object? properties)
    {
        if (properties is null) return;

        var targetType = _target.GetType();
        var inputProps = properties.GetType().GetProperties();

        foreach (var inputProp in inputProps)
        {
            string propName = inputProp.Name;
            var (Getter, Setter) = GetAccessors(targetType, propName);

            if (Getter is null || Setter is null) continue;

            float startVal = Getter(_target);
            float endVal = Convert.ToSingle(inputProp.GetValue(properties));

            _tracks.Add(new AnimationTrack(Setter, startVal, endVal));
        }
    }

    private static (Func<object, float> Getter, Action<object, float> Setter) GetAccessors(Type type, string propertyName)
    {
        if (_accessorCache.TryGetValue((type, propertyName), out var accessors))
        {
            return accessors;
        }

        var propertyInfo = type.GetProperty(propertyName);
        if (propertyInfo is null || propertyInfo.PropertyType != typeof(float) || !propertyInfo.CanRead || !propertyInfo.CanWrite)
        {
            accessors = (null!, null!);
        }
        else
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var castTarget = Expression.Convert(targetParam, type);
            var propertyAccess = Expression.Property(castTarget, propertyInfo);
            var getter = Expression.Lambda<Func<object, float>>(propertyAccess, targetParam).Compile();

            var valueParam = Expression.Parameter(typeof(float), "value");
            var assign = Expression.Assign(propertyAccess, valueParam);
            var setter = Expression.Lambda<Action<object, float>>(assign, targetParam, valueParam).Compile();

            accessors = (getter, setter);
        }

        _accessorCache.TryAdd((type, propertyName), accessors);
        return accessors;
    }

    // --- 缓动算法 (复制自 Animator 以保持独立性) ---
    private static float ApplyEasing(float t, EasingFunction easing)
    {
        return easing switch
        {
            EasingFunction.Linear => t,
            EasingFunction.CubicEaseIn => t * t * t,
            EasingFunction.CubicEaseOut => 1 - MathF.Pow(1 - t, 3),
            EasingFunction.CubicEaseInOut => t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2,
            EasingFunction.CircleEaseIn => 1 - MathF.Sqrt(1 - t * t),
            EasingFunction.CircleEaseOut => MathF.Sqrt(1 - MathF.Pow(t - 1, 2)),
            EasingFunction.CircleEaseInOut => t < 0.5f
                ? (1 - MathF.Sqrt(1 - MathF.Pow(2 * t, 2))) / 2
                : (MathF.Sqrt(1 - MathF.Pow(-2 * t + 2, 2)) + 1) / 2,
            _ => t
        };
    }
}
