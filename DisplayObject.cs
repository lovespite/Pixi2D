using Pixi2D.Events;
using SharpDX.Direct2D1;
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
    public float X { get; set; }
    public float Y { get; set; }

    public virtual float Height { get; set; } = 0.0f;
    public virtual float Width { get; set; } = 0.0f;

    public float Scale { set => ScaleX = ScaleY = value; }
    public float ScaleX { get; set; } = 1.0f;
    public float ScaleY { get; set; } = 1.0f;

    /// <summary>
    /// 锚点 X 坐标 (0.0 到 1.0，默认为 0)。
    /// 0.0 = 左边缘，0.5 = 中心，1.0 = 右边缘。
    /// 锚点定义了对象的变换原点（旋转、缩放的中心点）。
    /// </summary>
    public float AnchorX { get; set; } = 0.0f;

    /// <summary>
    /// 锚点 Y 坐标 (0.0 到 1.0，默认为 0)。
    /// 0.0 = 顶部边缘，0.5 = 中心，1.0 = 底部边缘。
    /// 锚点定义了对象的变换原点（旋转、缩放的中心点）。
    /// </summary>
    public float AnchorY { get; set; } = 0.0f;

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
    public Action<float>? OnUpate { get; set; }
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

        // 反转矩阵以获得 世界 -> 局部 的变换
        if (!Matrix3x2.Invert(currentTransform, out var worldToLocal))
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
    /// 旋转角度 (以弧度为单位)。
    /// </summary>
    public float Rotation { get; set; } = 0.0f;

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
    /// 计算此对象的局部变换矩阵。
    /// <para>已更改: 返回 Matrix3x2 (用于计算)。</para>
    /// <para>支持锚点: 变换会围绕 (Width * AnchorX, Height * AnchorY) 进行。</para>
    /// </summary>
    public virtual Matrix3x2 GetLocalTransform()
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
    /// 更新逻辑 (例如, 动画)。
    /// </summary>
    /// <param name="deltaTime">上一帧以来的时间 (秒)。</param>
    public virtual void Update(float deltaTime)
    {
        OnUpate?.Invoke(deltaTime);
    }

    /// <summary>
    /// 递归渲染此对象及其子对象。
    /// <para>已更改: 接受 Matrix3x2 (用于计算)。</para>
    /// </summary>
    /// <param name="renderTarget">D2D 渲染目标。</param>
    /// <param name="parentTransform">来自父容器的世界变换矩阵。</param>
    public abstract void Render(RenderTarget renderTarget, Matrix3x2 parentTransform);

    /// <summary>
    /// 释放此对象持有的所有非托管资源。
    /// </summary>
    public virtual void Dispose()
    {
        // 默认不执行任何操作，子类应重写它
        Parent?.RemoveChild(this);
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
