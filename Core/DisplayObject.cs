using Pixi2D.Core;
using Pixi2D.Events;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D;

// 使用 SharpDX.Direct2D1.Bitmap1 (来自 WIC) 或 SharpDX.Direct2D1.Bitmap (来自 DXGI)
// 为简单起见，这里我们假设是 Bitmap1

/// <summary>
/// 场景图中所有可渲染对象的基础抽象类。
/// 类似于 PIXI.js 中的 DisplayObject。
/// </summary>
public abstract class DisplayObject : IDisposable
{
    public string? Name { get; set; }
    // --- 变换属性 ---
    private float _x, _y, _scaleX = 1.0f, _scaleY = 1.0f, _anchorX = 0.0f, _anchorY = 0.0f, _rotation = 0.0f;
    private float _width = 0.0f, _height = 0.0f;

    // --- 变换缓存 (优化点) ---
    protected Matrix3x2 _localTransform;
    protected Matrix3x2 _worldTransform;
    protected bool _localDirty = true;
    protected bool _worldDirty = true;

    public event Action? OnDisposed;

    public object? Tag { get; set; } = null;

    public override string ToString()
    {
        return Name is null ? GetType().Name : $"<{GetType().Name}>:{Name}";
    }

    /// <summary>
    /// 跟踪父对象的世界变换版本
    /// </summary>
    protected uint _parentVersion = 0;
    /// <summary>
    /// 我们自己的世界变换版本
    /// </summary>
    internal uint _worldVersion = 1;
    // --- 

    public float X { get => _x; set { _x = value; Invalidate(); } }
    public float Y { get => _y; set { _y = value; Invalidate(); } }
    public PointF Position
    {
        get => new(X, Y);
        set { (X, Y) = (value.X, value.Y); Invalidate(); } // Invalidate 已在 X/Y setter 中
    }

    public virtual float Height { get => _height; set { _height = value; Invalidate(); } }
    public virtual float Width { get => _width; set { _width = value; Invalidate(); } }
    public virtual SizeF Size
    {
        get => new(Width, Height);
        set
        {
            (Width, Height) = (value.Width, value.Height);
            Invalidate();
        } // Invalidate 已在 Width/Height setter 中
    }

    public float Scale { set { ScaleX = ScaleY = value; } } // Setter 会调用 Invalidate
    public float ScaleX { get => _scaleX; set { _scaleX = value; Invalidate(); } }
    public float ScaleY { get => _scaleY; set { _scaleY = value; Invalidate(); } }

    /// <summary>
    /// 锚点 X 坐标 (0.0 到 1.0，默认为 0)。
    /// 0.0 = 左边缘，0.5 = 中心，1.0 = 右边缘。
    /// 锚点定义了对象的变换原点（旋转、缩放的中心点）。
    /// </summary>
    public float AnchorX { get => _anchorX; set { _anchorX = value; Invalidate(); } }

    /// <summary>
    /// 锚点 Y 坐标 (0.0 到 1.0，默认为 0)。
    /// 0.0 = 顶部边缘，0.5 = 中心，1.0 = 底部边缘。
    /// 锚点定义了对象的变换原点（旋转、缩放的中心点）。
    /// </summary>
    public float AnchorY { get => _anchorY; set { _anchorY = value; Invalidate(); } }
    public float Anchor { set { AnchorX = AnchorY = value; } } // Setter 会调用 Invalidate

    /// <summary>
    /// 旋转角度 (以弧度为单位)。
    /// </summary>
    public float Rotation { get => _rotation; set { _rotation = value; Invalidate(); } }

    /// <summary>
    /// 标记此对象的局部变换为“脏”，将在下一帧重新计算。
    /// </summary>
    public void Invalidate()
    {
        _localDirty = true;
        _worldDirty = true;
    }

    /// <summary>
    /// 计算此对象的局部变换矩阵 (仅在需要时)。
    /// </summary>
    protected virtual Matrix3x2 CalculateLocalTransform()
    {
        // 计算锚点的像素偏移
        float anchorOffsetX = Width * AnchorX;
        float anchorOffsetY = Height * AnchorY;

        // 变换顺序:
        // 1. 平移到锚点位置（使锚点成为原点）
        // 2. 缩放
        // 3. 旋转
        // 4. 平移回去并移动到最终位置
        return Matrix3x2.CreateTranslation(-anchorOffsetX, -anchorOffsetY) *
               Matrix3x2.CreateScale(ScaleX, ScaleY) *
               Matrix3x2.CreateRotation(Rotation) *
               Matrix3x2.CreateTranslation(X + anchorOffsetX, Y + anchorOffsetY);
    }

    /// <summary>
    /// 获取当前的绝对世界变换矩阵。
    /// 此方法会实时向上遍历父级链进行计算，确保矩阵是最新的。
    /// </summary>
    public Matrix3x2 GetWorldTransform()
    {
        // 从当前对象开始
        Matrix3x2 matrix = CalculateLocalTransform();

        DisplayObject? current = Parent;
        while (current is not null)
        {
            // 累乘父级的局部变换
            // 矩阵乘法顺序：Local * ParentLocal * GrandParentLocal ...
            matrix = matrix * current.CalculateLocalTransform();
            current = current.Parent;
        }

        return matrix;
    }

    /// <summary>
    /// Must be set to true for this object to receive events.
    /// (为 true 时此对象才能接收事件。)
    /// </summary>
    public bool Interactive { get; set; } = false;

    /// <summary>
    /// 是否允许此对象接受焦点。
    /// </summary>
    public bool AcceptFocus { get; set; } = false;

    /// <summary>
    /// 用于指定当此对象获得焦点时，焦点应转移到的目标对象，null 表示焦点留在此对象上。
    /// </summary>
    public DisplayObject? FocusTarget { get; set; } = null;

    public DisplayObject? FindFirstFocusableTarget()
    {
        if (FocusTarget is not null) return FocusTarget;
        if (AcceptFocus) return this;

        DisplayObject current = this;
        while (current.Parent is DisplayObject obj)
        {
            if (obj.FocusTarget is not null)
                return obj.FocusTarget;
            if (obj.AcceptFocus)
                return obj;

            current = obj;
        }

        return null;
    }

    // --- Event Handlers (事件处理器) ---
    public Action<DisplayObjectEvent>? OnClick { get; set; }
    public Action<DisplayObjectEvent>? OnMouseOver { get; set; }
    public Action<DisplayObjectEvent>? OnMouseOut { get; set; }
    public Action<DisplayObjectEvent>? OnMouseMove { get; set; }
    public Action<DisplayObjectEvent>? OnMouseDown { get; set; }
    public Action<DisplayObjectEvent>? OnMouseUp { get; set; }
    public Action<DisplayObjectEvent>? OnMouseWheel { get; set; }
    public Action<float>? OnUpdate { get; set; }
    /// <summary>
    /// 键盘按下事件 (需要对象有焦点)
    /// </summary>
    public Action<DisplayObjectEvent>? OnKeyDown { get; set; }

    /// <summary>
    /// 键盘抬起事件 (需要对象有焦点)
    /// </summary>
    public Action<DisplayObjectEvent>? OnKeyUp { get; set; }

    /// <summary>
    /// 字符输入事件 (需要对象有焦点)
    /// </summary>
    public Action<DisplayObjectEvent>? OnKeyPress { get; set; }

    public Action? OnFocus { get; set; }
    public Action? OnBlur { get; set; }

    /// <summary>
    /// 递归查找被世界坐标点命中的最顶层 DisplayObject。
    /// </summary>
    /// <param name="worldPoint">世界坐标点 (例如屏幕坐标)。</param>
    /// <param name="currentTransform">此对象的当前世界变换矩阵。</param>
    /// <param name="hitEvent">用于填充命中数据的事件对象。</param>
    /// <returns>被命中的 DisplayObject，如果未命中则为 null。</returns>
    internal virtual DisplayObject? FindHitObject(PointF worldPoint, Matrix3x2 currentTransform, DisplayObjectEvent hitEvent)
    {
        if (!Visible || !Interactive) return null;

        // HitTest 是一种“慢速路径”，它需要即时计算变换，
        // 而不能依赖可能陈旧的缓存。
        Matrix3x2 localTransform = CalculateLocalTransform();
        Matrix3x2 worldTransform = localTransform * currentTransform;


        // 反转矩阵以获得 世界 -> 局部 的变换
        if (!Matrix3x2.Invert(worldTransform, out var worldToLocal))
        {
            return null; // 矩阵不可逆
        }

        // 将世界点转换为局部点 
        PointF localPoint = new(
            worldToLocal.M11 * worldPoint.X + worldToLocal.M21 * worldPoint.Y + worldToLocal.M31,
            worldToLocal.M12 * worldPoint.X + worldToLocal.M22 * worldPoint.Y + worldToLocal.M32
        );
        // 检查这个对象是否被命中
        if (HitTest(localPoint))
        {
            // 是, 填充事件数据并返回
            hitEvent.Target = this;
            hitEvent.LocalPosition = localPoint;
            return this;
        }

        return null; // 未命中
    }

    /// <summary>
    /// 检查一个点 (在*本地*坐标系中) 是否在此对象的几何形状内。
    /// </summary>
    /// <param name="localPoint">本地坐标系中的点。</param>
    /// <returns>如果命中则为 true。</returns>
    public abstract bool HitTest(PointF localPoint);

    /// <summary>
    /// 透明度 (0.0 到 1.0)。
    /// </summary>
    public float Alpha { get; set; } = 1.0f;

    public bool Visible { get; set; } = true;

    /// <summary>
    /// 对父容器的引用。
    /// </summary>
    public Container? Parent { get; internal set; }

    /// <summary>
    /// *（已弃用）计算此对象的局部变换矩阵。
    /// * 请改用 CalculateLocalTransform()。
    /// </summary>
    [Obsolete("Use CalculateLocalTransform() for internal calculations or rely on the cached transform during render.", true)]
    public virtual Matrix3x2 GetLocalTransform()
    {
        // 此方法保留（标记为 Obsolete）以防外部依赖，
        // 但内部应使用 CalculateLocalTransform() 和缓存。
        return CalculateLocalTransform();
    }

    /// <summary>
    /// 更新逻辑 (例如, 动画)。
    /// </summary>
    /// <param name="deltaTime">上一帧以来的时间 (秒)。</param>
    public virtual void Update(float deltaTime)
    {
        OnUpdate?.Invoke(deltaTime);
    }

    /// <summary>
    /// 递归渲染此对象及其子对象。
    /// <para>已更改: 接受 Matrix3x2 (用于计算)。</para>
    /// </summary>
    /// <param name="renderTarget">D2D 渲染目标。</param>
    /// <param name="parentTransform">来自父容器的世界变换矩阵。</param>
    public abstract void Render(RenderTarget renderTarget, ref Matrix3x2 parentTransform);

    /// <summary>
    /// 释放此对象持有的所有非托管资源。
    /// </summary>
    public virtual void Dispose()
    {
        // 默认不执行任何操作，子类应重写它
        Parent?.RemoveChild(this);
        Tag = null;

        OnDisposed?.Invoke();
    }

    #region Helper Methods

    public DisplayObject SetSize(float w, float h)
    {
        Width = w;
        Height = h;
        return this;
    }

    public DisplayObject SetPosition(float x, float y)
    {
        X = x;
        Y = y;
        return this;
    }

    /// <summary>
    /// 设置锚点。
    /// </summary>
    /// <param name="anchorX">锚点 X 坐标 (0.0 到 1.0)。</param>
    /// <param name="anchorY">锚点 Y 坐标 (0.0 到 1.0)。</param>
    /// <returns>当前对象，用于链式调用。</returns>
    public DisplayObject SetAnchor(float anchorX, float anchorY)
    {
        AnchorX = anchorX;
        AnchorY = anchorY;
        return this;
    }

    /// <summary>
    /// 设置锚点为中心 (0.5, 0.5)。
    /// </summary>
    /// <returns>当前对象，用于链式调用。</returns>
    public DisplayObject SetAnchorCenter()
    {
        return SetAnchor(0.5f, 0.5f);
    }

    /// <summary>
    /// 查找当前所在的 Stage。
    /// </summary>
    public Stage? GetStage()
    {
        DisplayObject? current = this;
        while (current is not null)
        {
            if (current is Stage stage)
                return stage;
            current = current.Parent;
        }
        return null;
    }

    public bool IsFocused()
    {
        var stage = GetStage();
        if (stage is null) return false;
        return ReferenceEquals(stage.FocusedObject, this);
    }

    public void Focus()
    {
        var stage = GetStage();
        if (stage is null) return;
        stage.SetFocus(this);
    }

    #endregion
}
