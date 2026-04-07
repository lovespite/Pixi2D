namespace Pixi2D.Components.Draggable;


public interface IDraggable
{
    bool IsDragging { get; }
    DisplayObject AsDisplayObject();
}

public interface IDraggableContainer
{
    // 当元素成功放入此容器时触发
    event Action<IDraggableContainer, IDraggable> ItemChanged;

    // 检查此容器是否允许该元素放入
    bool CanAcceptDrop(IDraggable item);

    // 接受掉落，并计算/分配元素最终在容器中的局部坐标
    System.Drawing.PointF AcceptDrop(IDraggable item, System.Drawing.PointF dropWorldPosition);

    DisplayObject AsDisplayObject();
}