using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Pixi2D.Core;

namespace Pixi2D.Core;

/// <summary>
/// 定义动画的缓动算法类型。
/// </summary>
public enum EasingFunction
{
    Linear,
    /// <summary>
    /// 立方缓动 - 渐入
    /// </summary>
    CubicEaseIn,
    /// <summary>
    /// 立方缓动 - 渐出
    /// </summary>
    CubicEaseOut,
    /// <summary>
    /// 立方缓动 - 渐入渐出
    /// </summary>
    CubicEaseInOut,
    /// <summary>
    /// 圆形缓动 - 渐入
    /// </summary>
    CircleEaseIn,
    /// <summary>
    /// 圆形缓动 - 渐出
    /// </summary>
    CircleEaseOut,
    /// <summary>
    /// 圆形缓动 - 渐入渐出
    /// </summary>
    CircleEaseInOut
}

/// <summary>
/// 动画辅助类。
/// 用于对 DisplayObject 的属性进行缓动动画。
/// <para>
/// 优化说明：使用 Expression Tree 编译属性访问器并进行静态缓存，
/// 替代了每帧使用 Reflection 的方式，大幅提高了性能。
/// </para>
/// </summary>
public class Animator
{
    // --- 静态缓存 ---
    // 缓存键：(目标类型, 属性名)
    // 缓存值：(Getter委托, Setter委托)
    private static readonly ConcurrentDictionary<(Type, string), (Func<object, float> Getter, Action<object, float> Setter)> _accessorCache = new();

    // --- 实例字段 ---
    private readonly DisplayObject _target;

    // 存储动画轨道：Setter委托, 初始值, 结束值
    private readonly List<AnimationTrack> _tracks = new();

    private readonly float _duration;
    private readonly float _totalDelay;
    private readonly EasingFunction _easing;

    private float _elapsedTime; // 动画已运行时间（不含Delay）
    private float _delayTimer;  // 倒计时 Delay
    private bool _isPlaying;

    // 任务源，用于 Task 属性
    private readonly TaskCompletionSource<bool> _tcs = new();

    /// <summary>
    /// 内部结构，用于存储单条属性的动画信息
    /// </summary>
    private readonly struct AnimationTrack
    {
        public readonly Action<object, float> Setter;
        public readonly float StartValue;
        public readonly float EndValue;

        public AnimationTrack(Action<object, float> setter, float startValue, float endValue)
        {
            Setter = setter;
            StartValue = startValue;
            EndValue = endValue;
        }
    }

    /// <summary>
    /// 动画完成时触发的事件。
    /// </summary>
    public event Action? OnCompleted;

    /// <summary>
    /// 表示整个动画任务。
    /// 可以 await 此属性等待动画结束。
    /// </summary>
    public Task Task => _tcs.Task;

    /// <summary>
    /// 构造一个新的动画器并自动开始播放。
    /// </summary>
    /// <param name="target">动画的目标对象。</param>
    /// <param name="properties">包含目标属性和结束值的匿名对象 (例如: new { X = 100, Alpha = 0 })。</param>
    /// <param name="duration">动画持续时间 (秒)。</param>
    /// <param name="easing">缓动算法。</param>
    /// <param name="delay">启动延迟时间 (秒)。</param>
    public Animator(DisplayObject target, object properties, float duration, EasingFunction easing = EasingFunction.Linear, float delay = 0f)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _duration = Math.Max(0, duration);
        _easing = easing;
        _totalDelay = Math.Max(0, delay);
        _delayTimer = _totalDelay;
        _elapsedTime = 0f;

        // 解析属性并构建动画轨道
        ParseProperties(properties);

        // 挂载到目标对象的更新循环
        _target.OnUpate += Update;
        _isPlaying = true;
    }

    /// <summary>
    /// 停止动画，并保持当前状态。
    /// Task 将被标记为 Canceled。
    /// </summary>
    public void Stop()
    {
        if (!_isPlaying) return;

        Cleanup();
        _tcs.TrySetCanceled();
    }

    /// <summary>
    /// 停止动画，并将元素恢复到最初状态。
    /// Task 将被标记为 Canceled。
    /// </summary>
    public void Cancel()
    {
        if (!_isPlaying && _tcs.Task.IsCompleted) return;

        // 恢复初始值
        foreach (var track in _tracks)
        {
            track.Setter(_target, track.StartValue);
        }

        if (_isPlaying)
        {
            Cleanup();
            _tcs.TrySetCanceled();
        }
    }

    // --- 内部逻辑 ---

    private void ParseProperties(object properties)
    {
        if (properties == null) return;

        var targetType = _target.GetType();
        // 获取匿名对象的所有属性（即用户想要动画的属性名和目标值）
        var inputProps = properties.GetType().GetProperties();

        foreach (var inputProp in inputProps)
        {
            string propName = inputProp.Name;

            // 1. 获取或编译访问器 (Getter/Setter)
            var (Getter, Setter) = GetAccessors(targetType, propName);

            // 如果 accessors 为默认值，说明目标对象上没有这个 float 属性，或者不可读写
            if (Getter == null || Setter == null)
                continue;

            // 2. 获取当前值作为 StartValue
            float startVal = Getter(_target);

            // 3. 获取目标值作为 EndValue
            // 注意：这里仍需用到一次反射来从匿名对象中取值，但仅在构造时发生一次
            float endVal = Convert.ToSingle(inputProp.GetValue(properties));

            // 4. 添加到轨道列表
            _tracks.Add(new AnimationTrack(Setter, startVal, endVal));
        }
    }

    /// <summary>
    /// 获取指定类型的属性访问器，如果缓存中不存在则编译。
    /// </summary>
    private static (Func<object, float> Getter, Action<object, float> Setter) GetAccessors(Type type, string propertyName)
    {
        // 尝试从缓存获取
        if (_accessorCache.TryGetValue((type, propertyName), out var accessors))
        {
            return accessors;
        }

        // 编译新的访问器
        var propertyInfo = type.GetProperty(propertyName);

        // 验证属性是否存在且为 float 类型且可读写
        if (propertyInfo == null ||
            propertyInfo.PropertyType != typeof(float) ||
            !propertyInfo.CanRead ||
            !propertyInfo.CanWrite)
        {
            // 存入无效值以避免重复查找
            accessors = (null!, null!);
        }
        else
        {
            // --- 编译 Getter: (object target) => (float)((TargetType)target).Property ---
            var targetParam = Expression.Parameter(typeof(object), "target");
            var castTarget = Expression.Convert(targetParam, type);
            var propertyAccess = Expression.Property(castTarget, propertyInfo);
            var getterLambda = Expression.Lambda<Func<object, float>>(propertyAccess, targetParam);
            var getter = getterLambda.Compile();

            // --- 编译 Setter: (object target, float value) => ((TargetType)target).Property = value ---
            var valueParam = Expression.Parameter(typeof(float), "value");
            var assign = Expression.Assign(propertyAccess, valueParam);
            var setterLambda = Expression.Lambda<Action<object, float>>(assign, targetParam, valueParam);
            var setter = setterLambda.Compile();

            accessors = (getter, setter);
        }

        // 存入缓存
        _accessorCache.TryAdd((type, propertyName), accessors);
        return accessors;
    }

    private void Update(float deltaTime)
    {
        if (!_isPlaying) return;

        // 1. 处理延迟
        if (_delayTimer > 0)
        {
            _delayTimer -= deltaTime;
            if (_delayTimer > 0) return;

            // 修正 deltaTime
            deltaTime = -_delayTimer;
            _delayTimer = 0;
        }

        // 2. 更新时间
        _elapsedTime += deltaTime;

        // 3. 计算进度 (0.0 ~ 1.0)
        float t = 0f;
        if (_duration > 0)
        {
            t = Math.Clamp(_elapsedTime / _duration, 0f, 1f);
        }
        else
        {
            t = 1f;
        }

        // 4. 应用缓动算法
        float factor = ApplyEasing(t, _easing);

        // 5. 更新所有属性 (使用编译后的委托，速度极快)
        // 使用 for 循环遍历 List 比 foreach 稍微快一点点，且无 GC
        var count = _tracks.Count;
        for (int i = 0; i < count; i++)
        {
            var track = _tracks[i];
            float current = track.StartValue + (track.EndValue - track.StartValue) * factor;
            track.Setter(_target, current);
        }

        // 6. 检查完成
        if (t >= 1f)
        {
            Complete();
        }
    }

    private void Complete()
    {
        Cleanup();
        OnCompleted?.Invoke();
        _tcs.TrySetResult(true);
    }

    private void Cleanup()
    {
        _isPlaying = false;
        _target.OnUpate -= Update;
    }

    // --- 缓动算法实现 ---

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

    public Animator ContinueWith(Action<Animator> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        Task.ContinueWith(_ => continuation(this), TaskScheduler.FromCurrentSynchronizationContext());
        return this;
    }

    public Animator ContinueWith(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Task.ContinueWith(_ => action(), TaskScheduler.FromCurrentSynchronizationContext());
        return this;
    }
}
