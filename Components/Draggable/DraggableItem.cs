using Pixi2D.Core;
using Pixi2D.Events;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Components.Draggable;

public class DraggableItem : Container, IDraggable
{
    public bool IsDragging { get; private set; }

    private Container? _originalParent;
    private PointF _originalLocalPosition;
    private PointF _dragOffset;
    private Stage? _stage;

    public DraggableItem()
    {
        Interactive = true; // 必须开启以接收鼠标事件
        OnMouseDown = HandleMouseDown;
    }

    public override bool HitTest(PointF localPoint)
    {
        return localPoint.X >= 0 && localPoint.X <= Width && localPoint.Y >= 0 && localPoint.Y <= Height;
    }

    public DisplayObject AsDisplayObject() => this;

    private void HandleMouseDown(DisplayObjectEvent e)
    {
        if (IsDragging) return;

        _stage = GetStage();
        if (_stage == null) return;

        IsDragging = true;
        _originalParent = Parent;
        _originalLocalPosition = Position;

        // 计算鼠标点到控件左上角的偏移量（世界坐标下）
        PointF worldPos = ToWorldPoint(0, 0);
        _dragOffset = new PointF(e.WorldPosition.X - worldPos.X, e.WorldPosition.Y - worldPos.Y);

        // 状态提升：移出原父级，加入 Stage 以确保渲染在最顶层
        _originalParent?.RemoveChild(this);
        _stage.AddChild(this);
        Position = worldPos;

        // 监听全局移动和抬起事件
        _stage.OnMouseMove += HandleStageMouseMove;
        _stage.OnMouseUp += HandleStageMouseUp;

        // 【可选动画】：拖拽抓起时稍微放大
        new Animator(this, new { ScaleX = 1.1f, ScaleY = 1.1f }, 0.15f, EasingFunction.CubicEaseOut);
    }

    private void HandleStageMouseMove(DisplayObjectEvent e)
    {
        if (!IsDragging) return;

        // 跟随鼠标移动（保持偏移量）
        X = e.WorldPosition.X - _dragOffset.X;
        Y = e.WorldPosition.Y - _dragOffset.Y;
    }

    private void HandleStageMouseUp(DisplayObjectEvent e)
    {
        if (!IsDragging || _stage == null) return;
        IsDragging = false;

        _stage.OnMouseMove -= HandleStageMouseMove;
        _stage.OnMouseUp -= HandleStageMouseUp;

        // 1. 临时关闭交互，以便 HitTest 穿透自身检测下方的容器
        Interactive = false; 
        DisplayObject? hitObj = _stage.FindHitObject(e.WorldPosition, Matrix3x2.Identity, e);
        Interactive = true;

        // 2. 向上冒泡查找实现了 IDraggableContainer 的容器
        IDraggableContainer? targetContainer = null;
        DisplayObject? current = hitObj;
        while (current != null)
        {
            if (current is IDraggableContainer c)
            {
                targetContainer = c;
                break;
            }
            current = current.Parent;
        }

        // 3. 判断是否可以放入目标容器
        if (targetContainer != null && targetContainer.CanAcceptDrop(this))
        {
            // 目标容器计算出分配的局部坐标，并触发 ItemChanged
            PointF targetLocalPos = targetContainer.AcceptDrop(this, e.WorldPosition);
            AnimateDropSuccess(targetContainer, targetLocalPos);
        }
        else
        {
            // 放置失败，弹回原处
            AnimateRevert();
        }
    }

    private void AnimateDropSuccess(IDraggableContainer targetContainer, PointF targetLocalPos)
    {
        DisplayObject targetObj = targetContainer.AsDisplayObject();

        // 计算目标在世界坐标系的绝对位置，以便我们在 Stage 层级进行流畅飞行
        Matrix3x2 targetWorldTransform = targetObj.GetWorldTransform();
        float targetWorldX = targetWorldTransform.M11 * targetLocalPos.X + targetWorldTransform.M21 * targetLocalPos.Y + targetWorldTransform.M31;
        float targetWorldY = targetWorldTransform.M12 * targetLocalPos.X + targetWorldTransform.M22 * targetLocalPos.Y + targetWorldTransform.M32;

        var anim = new Animator(this, new { X = targetWorldX, Y = targetWorldY, ScaleX = 1f, ScaleY = 1f }, 0.2f, EasingFunction.CubicEaseOut);
        anim.OnCompleted += () =>
        {
            // 动画结束后，正式更改父子层级关系
            Parent?.RemoveChild(this);
            ((Container)targetObj).AddChild(this);
            Position = targetLocalPos;
        };
    }

    private void AnimateRevert()
    {
        if (_originalParent == null) return;

        // 计算原父级中的原始位置对应的世界坐标
        PointF originalWorldPos = _originalParent.ToWorldPoint(_originalLocalPosition.X, _originalLocalPosition.Y);

        var anim = new Animator(this, new { originalWorldPos.X, originalWorldPos.Y, ScaleX = 1f, ScaleY = 1f }, 0.3f, EasingFunction.CubicEaseInOut);
        anim.OnCompleted += () =>
        {
            Parent?.RemoveChild(this);
            _originalParent.AddChild(this);
            Position = _originalLocalPosition;
        };
    }
}