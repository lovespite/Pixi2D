using System.Drawing;
using System.Numerics;

namespace Pixi2D.Controls;

/// <summary>
/// 流式佈局容器。
/// (A flow layout container.)
/// 支持水平或垂直流，並可通過添加 AddBreak() 來手動換行/換列。 
/// </summary>
public class FlowLayout : Container
{
    /// <summary>
    /// 列表項的排列方向。
    /// (Layout direction for items.)
    /// </summary>
    public enum LayoutDirection
    {
        Vertical,   // 垂直排列 (Vertical layout)
        Horizontal  // 水平排列 (Horizontal layout)
    }

    private LayoutDirection _direction = LayoutDirection.Horizontal;
    private float _gap = 0f;
    private float _paddingLeft = 0f;
    private float _paddingTop = 0f;
    private float _paddingRight = 0f;
    private float _paddingBottom = 0f;

    /// <summary>
    /// 佈局方向（主軸）。
    /// (Layout direction (main axis).)
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
    /// 元素之間的間距（在主軸和交叉轴上均适用）。
    /// (Spacing between elements (applies to both main and cross axis).)
    /// </summary>
    public float Gap
    {
        get => _gap;
        set
        {
            if (_gap != value)
            {
                _gap = value;
                UpdateLayout();
            }
        }
    }

    #region Padding Properties (内邊距屬性)

    /// <summary>
    /// 内邊距 - 左侧。
    /// (Padding - Left.)
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
    /// 内邊距 - 顶部。
    /// (Padding - Top.)
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
    /// 内邊距 - 右侧。
    /// (Padding - Right.)
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
    /// 内邊距 - 底部。
    /// (Padding - Bottom.)
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
    /// 設置所有内邊距為相同值。
    /// (Set all paddings to the same value.)
    /// </summary>
    public void SetPadding(float padding)
    {
        _paddingLeft = _paddingTop = _paddingRight = _paddingBottom = padding;
        UpdateLayout();
    }

    /// <summary>
    /// 設置内邊距 (上下相同，左右相同)。
    /// (Set padding (vertical, horizontal).)
    /// </summary>
    public void SetPadding(float vertical, float horizontal)
    {
        _paddingTop = _paddingBottom = vertical;
        _paddingLeft = _paddingRight = horizontal;
        UpdateLayout();
    }

    /// <summary>
    /// 設置内邊距 (左、上、右、下)。
    /// (Set padding (left, top, right, bottom).)
    /// </summary>
    public void SetPadding(float left, float top, float right, float bottom)
    {
        _paddingLeft = left;
        _paddingTop = top;
        _paddingRight = right;
        _paddingBottom = bottom;
        UpdateLayout();
    }

    #endregion

    #region Child Management (子项管理)
    // (重寫所有修改子項的方法，以便在佈局更改時自動調用 UpdateLayout)
    // (Override all child-modifying methods to automatically call UpdateLayout on change)

    /// <summary>
    /// 添加子項並自動更新佈局。
    /// (Add a child and update layout.)
    /// </summary>
    public new void AddChild(DisplayObject child)
    {
        base.AddChild(child);
        UpdateLayout();
    }

    /// <summary>
    /// 批量添加子項並更新佈局。
    /// (Add multiple children and update layout.)
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
    /// 移除子項並自動更新佈局。
    /// (Remove a child and update layout.)
    /// </summary>
    public new void RemoveChild(DisplayObject child)
    {
        base.RemoveChild(child);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定位置插入子項並更新佈局。
    /// (Insert a child at index and update layout.)
    /// </summary>
    public new void InsertChildAt(DisplayObject child, int index)
    {
        base.InsertChildAt(child, index);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定子項之前插入。
    /// (Insert a child before another.)
    /// </summary>
    public new void InsertBefore(DisplayObject child, DisplayObject? beforeChild)
    {
        base.InsertBefore(child, beforeChild);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定子項之後插入。
    /// (Insert a child after another.)
    /// </summary>
    public new void InsertAfter(DisplayObject child, DisplayObject? afterChild)
    {
        base.InsertAfter(child, afterChild);
        UpdateLayout();
    }

    /// <summary>
    /// 清除所有子項。
    /// (Clear all children.)
    /// </summary>
    public new void ClearChildren()
    {
        base.ClearChildren();
        UpdateLayout();
    }

    #endregion

    /// <summary>
    /// 重新計算所有子項的位置。
    /// (Recalculate positions of all children.)
    /// </summary>
    public void UpdateLayout()
    {
        if (children.Count == 0) return;

        float primaryPos = 0f;  // 主軸上的當前位置 (e.g., X for horizontal)
        float crossPos = 0f;    // 交叉軸上的當前位置 (e.g., Y for horizontal)
        float maxCrossSizeOnLine = 0f; // 當前行/列在交叉轴上的最大尺寸

        if (_direction == LayoutDirection.Horizontal)
        {
            // 水平流: X 是主軸, Y 是交叉軸
            // (Horizontal flow: X is main axis, Y is cross axis)
            primaryPos = _paddingLeft;
            crossPos = _paddingTop;

            foreach (var child in children)
            {
                if (child is FlowBreak)
                {
                    // 遇到換行符 (Encountered a line break)
                    primaryPos = _paddingLeft;
                    crossPos += maxCrossSizeOnLine + (maxCrossSizeOnLine > 0 ? _gap : 0);
                    maxCrossSizeOnLine = 0f;
                }
                else if (child.Visible) // 僅佈局可見元素 (Only layout visible elements)
                {
                    // 放置元素 (Place element)
                    child.X = primaryPos;
                    child.Y = crossPos;

                    // 更新主軸位置 (Update main axis position)
                    primaryPos += child.Width + _gap;
                    // 更新當前行的最大交叉轴尺寸 (Update max cross-axis size for current line)
                    if (child.Height > maxCrossSizeOnLine)
                    {
                        maxCrossSizeOnLine = child.Height;
                    }
                }
            }
        }
        else // Vertical
        {
            // 垂直流: Y 是主軸, X 是交叉軸
            // (Vertical flow: Y is main axis, X is cross axis)
            primaryPos = _paddingTop;
            crossPos = _paddingLeft;

            foreach (var child in children)
            {
                if (child is FlowBreak)
                {
                    // 遇到換列符 (Encountered a column break)
                    primaryPos = _paddingTop;
                    crossPos += maxCrossSizeOnLine + (maxCrossSizeOnLine > 0 ? _gap : 0);
                    maxCrossSizeOnLine = 0f;
                }
                else if (child.Visible) // 僅佈局可見元素 (Only layout visible elements)
                {
                    // 放置元素 (Place element)
                    child.X = crossPos;
                    child.Y = primaryPos;

                    // 更新主軸位置 (Update main axis position)
                    primaryPos += child.Height + _gap;
                    // 更新當前列的最大交叉轴尺寸 (Update max cross-axis size for current column)
                    if (child.Width > maxCrossSizeOnLine)
                    {
                        maxCrossSizeOnLine = child.Width;
                    }
                }
            }
        }
    }

    public void AddBreak()
    {
        AddChild(FlowBreak.Instance);
    }

    public static FlowBreak Break => FlowBreak.Instance;

    /// <summary>
    /// 一個用於 FlowList 的標記对象，表示換行（或換列）。
    /// (A marker object for FlowList to indicate a line break (or column break).)
    /// 這是一個輕量級對象，它本身不可見，也不參與點擊測試。
    /// (This is a lightweight object that is not visible and does not participate in hit testing.)
    /// </summary>
    public sealed class FlowBreak : DisplayObject
    {
        public override bool HitTest(PointF localPoint) => false;
        public override void Render(SharpDX.Direct2D1.RenderTarget renderTarget, Matrix3x2 parentTransform)
        {
            // This object does not render anything.
            // (此對象不渲染任何內容。)
        }

        private FlowBreak() { }

        public static FlowBreak Instance { get; } = new();
    }
}

