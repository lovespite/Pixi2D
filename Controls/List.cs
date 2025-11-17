using Pixi2D.Core;

namespace Pixi2D.Controls;

/// <summary>
/// 列表控件，支持动态添加、删除和自动布局子项。
/// 类似于 WPF 的 ListBox 或 StackPanel。
/// 此控件已过时，建议使用更强大的 <see cref="FlowLayout"/> 控件代替。
/// </summary>
[Obsolete("List 控件已过时，请使用更强大的 FlowLayout 控件代替。")]
public class List : Container
{
    /// <summary>
    /// 列表项的排列方向。
    /// </summary>
    public enum LayoutDirection
    {
        Vertical,   // 垂直排列 (从上到下)
        Horizontal  // 水平排列 (从左到右)
    }

    private LayoutDirection _direction = LayoutDirection.Vertical;
    private float _itemSpacing = 0f;
    private float _paddingLeft = 0f;
    private float _paddingTop = 0f;
    private float _paddingRight = 0f;
    private float _paddingBottom = 0f;

    /// <summary>
    /// 列表项的排列方向。
    /// </summary>
    public LayoutDirection Direction
    {
        get => _direction;
        set
        {
            if (_direction != value)
            {
                _direction = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 列表项之间的间距 (像素)。
    /// </summary>
    public float ItemSpacing
    {
        get => _itemSpacing;
        set
        {
            if (_itemSpacing != value)
            {
                _itemSpacing = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 内边距 - 左侧。
    /// </summary>
    public float PaddingLeft
    {
        get => _paddingLeft;
        set
        {
            if (_paddingLeft != value)
            {
                _paddingLeft = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 内边距 - 顶部。
    /// </summary>
    public float PaddingTop
    {
        get => _paddingTop;
        set
        {
            if (_paddingTop != value)
            {
                _paddingTop = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 内边距 - 右侧。
    /// </summary>
    public float PaddingRight
    {
        get => _paddingRight;
        set
        {
            if (_paddingRight != value)
            {
                _paddingRight = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 内边距 - 底部。
    /// </summary>
    public float PaddingBottom
    {
        get => _paddingBottom;
        set
        {
            if (_paddingBottom != value)
            {
                _paddingBottom = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// 设置所有内边距为相同值。
    /// </summary>
    public void SetPadding(float padding)
    {
        _paddingLeft = _paddingTop = _paddingRight = _paddingBottom = padding;
        UpdateLayout();
    }

    /// <summary>
    /// 设置内边距 (上下相同，左右相同)。
    /// </summary>
    public void SetPadding(float vertical, float horizontal)
    {
        _paddingTop = _paddingBottom = vertical;
        _paddingLeft = _paddingRight = horizontal;
        UpdateLayout();
    }

    /// <summary>
    /// 设置内边距 (左、上、右、下)。
    /// </summary>
    public void SetPadding(float left, float top, float right, float bottom)
    {
        _paddingLeft = left;
        _paddingTop = top;
        _paddingRight = right;
        _paddingBottom = bottom;
        UpdateLayout();
    }

    /// <summary>
    /// 添加子项并自动更新布局。
    /// </summary>
    public new void AddChild(DisplayObject child)
    {
        base.AddChild(child);
        UpdateLayout();
    }

    /// <summary>
    /// 批量添加子项并更新布局 (比逐个添加更高效)。
    /// </summary>
    public new void AddChildren(params DisplayObject[] newChildren)
    {
        foreach (var child in newChildren)
        {
            base.AddChild(child);
        }
        UpdateLayout();
    }

    /// <summary>
    /// 移除子项并自动更新布局。
    /// </summary>
    public new void RemoveChild(DisplayObject child)
    {
        base.RemoveChild(child);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定位置插入子项并更新布局。
    /// </summary>
    public new void InsertChildAt(DisplayObject child, int index)
    {
        base.InsertChildAt(child, index);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定子项之前插入。
    /// </summary>
    public new void InsertBefore(DisplayObject child, DisplayObject? beforeChild)
    {
        base.InsertBefore(child, beforeChild);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定子项之后插入。
    /// </summary>
    public new void InsertAfter(DisplayObject child, DisplayObject? afterChild)
    {
        base.InsertAfter(child, afterChild);
        UpdateLayout();
    }

    /// <summary>
    /// 清除所有子项。
    /// </summary>
    public new void ClearChildren()
    {
        base.ClearChildren();
        UpdateLayout();
    }

    /// <summary>
    /// 根据当前的方向和间距重新计算所有子项的位置。
    /// </summary>
    public void UpdateLayout()
    {
        if (children.Count == 0) return;

        float currentPos = 0f;

        if (_direction == LayoutDirection.Vertical)
        {
            // 垂直布局: 从上到下排列
            currentPos = _paddingTop;
            foreach (var child in children)
            {
                child.X = _paddingLeft;
                child.Y = currentPos;
                currentPos += child.Height + _itemSpacing;
            }
        }
        else
        {
            // 水平布局: 从左到右排列
            currentPos = _paddingLeft;
            foreach (var child in children)
            {
                child.X = currentPos;
                child.Y = _paddingTop;
                currentPos += child.Width + _itemSpacing;
            }
        }
    }

    /// <summary>
    /// 获取列表项的数量。
    /// </summary>
    public int ItemCount => children.Count;

    /// <summary>
    /// 通过索引获取列表项。
    /// </summary>
    public DisplayObject? GetItemAt(int index)
    {
        if (index < 0 || index >= children.Count)
            return null;
        return children[index];
    }

    /// <summary>
    /// 获取指定子项的索引。
    /// </summary>
    public int GetItemIndex(DisplayObject child)
    {
        return children.IndexOf(child);
    }

    /// <summary>
    /// 计算列表的总内容尺寸 (包括内边距)。
    /// </summary>
    public (float width, float height) GetContentSize()
    {
        if (children.Count == 0)
            return (_paddingLeft + _paddingRight, _paddingTop + _paddingBottom);

        float totalWidth = _paddingLeft + _paddingRight;
        float totalHeight = _paddingTop + _paddingBottom;

        if (_direction == LayoutDirection.Vertical)
        {
            // 垂直布局: 宽度取最大，高度累加
            float maxWidth = 0f;
            float sumHeight = 0f;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.Width > maxWidth)
                    maxWidth = child.Width;
                sumHeight += child.Height;
                if (i > 0)
                    sumHeight += _itemSpacing;
            }

            totalWidth += maxWidth;
            totalHeight += sumHeight;
        }
        else
        {
            // 水平布局: 宽度累加，高度取最大
            float sumWidth = 0f;
            float maxHeight = 0f;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                sumWidth += child.Width;
                if (child.Height > maxHeight)
                    maxHeight = child.Height;
                if (i > 0)
                    sumWidth += _itemSpacing;
            }

            totalWidth += sumWidth;
            totalHeight += maxHeight;
        }

        return (totalWidth, totalHeight);
    }

    /// <summary>
    /// 获取指定索引位置的项的位置坐标 (X 或 Y，取决于布局方向)。
    /// </summary>
    /// <param name="index">项的索引。</param>
    /// <returns>返回该项在主轴方向上的位置，如果索引无效则返回 null。</returns>
    public float? GetItemPosition(int index)
    {
        if (index < 0 || index >= children.Count)
            return null;

        float position = _direction == LayoutDirection.Vertical ? _paddingTop : _paddingLeft;

        for (int i = 0; i < index; i++)
        {
            var child = children[i];
            position += (_direction == LayoutDirection.Vertical ? child.Height : child.Width) + _itemSpacing;
        }

        return position;
    }
}
