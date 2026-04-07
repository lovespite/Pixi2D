using Pixi2D.Core;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Components.Draggable;

public class DropZoneContainer : Container, IDraggableContainer
{
    public event Action<IDraggableContainer, IDraggable>? ItemChanged;

    public DropZoneContainer()
    {
        Interactive = true; // 必须开启以允许 HitTest 生效
    }

    public DisplayObject AsDisplayObject() => this;

    public virtual bool CanAcceptDrop(IDraggable item)
    {
        return true; // 可以在这里添加业务逻辑判断，例如类型限制或容量限制
    }

    public virtual PointF AcceptDrop(IDraggable item, PointF dropWorldPosition)
    {
        // 计算鼠标释放位置转换为当前容器内部的局部坐标
        Matrix3x2 worldTransform = GetWorldTransform();
        if (Matrix3x2.Invert(worldTransform, out Matrix3x2 worldToLocal))
        {
            PointF localPoint = new(
                worldToLocal.M11 * dropWorldPosition.X + worldToLocal.M21 * dropWorldPosition.Y + worldToLocal.M31,
                worldToLocal.M12 * dropWorldPosition.X + worldToLocal.M22 * dropWorldPosition.Y + worldToLocal.M32
            );

            // 触发事件通知业务层级
            ItemChanged?.Invoke(this, item);

            return localPoint; // 或者返回一个由 Layout 固定的排版坐标
        }

        return new PointF(0, 0);
    }

    public override bool HitTest(PointF localPoint)
    {
        // 默认 Container 本身是透明无形状的，必须提供边界校验才能被查找到
        return localPoint.X >= 0 && localPoint.X <= Width &&
               localPoint.Y >= 0 && localPoint.Y <= Height;
    }
}