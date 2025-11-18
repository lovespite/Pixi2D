using Pixi2D.Core;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Controls;

/// <summary>
/// 流式佈局容器。 
/// 支持水平或垂直流，並可通過添加 AddBreak() 來手動換行/換列。 
/// (新功能) 支持主轴 (JustifyMain) 和交叉轴 (AlignCross) 对齐。 
/// </summary>
public class FlowLayout : Container
{
    #region Enums for Alignment (对齐枚举)

    /// <summary>
    /// 主轴对齐方式 
    /// </summary>
    public enum JustifyContent
    {
        /// <summary>
        /// 元素靠主轴起点对齐 
        /// </summary>
        Start,
        /// <summary>
        /// 元素靠主轴终点对齐 
        /// </summary>
        End,
        /// <summary>
        /// 元素在主轴上居中 
        /// </summary>
        Center,
        /// <summary>
        /// 元素在主轴上均匀分布，首尾元素贴边 
        /// </summary>
        SpaceBetween
    }

    /// <summary>
    /// 交叉轴对齐方式 
    /// </summary>
    public enum AlignItems
    {
        /// <summary>
        /// 元素靠交叉轴起点对齐 
        /// </summary>
        Start,
        /// <summary>
        /// 元素靠交叉轴终点对齐
        /// </summary>
        End,
        /// <summary>
        /// 元素在交叉轴上居中
        /// </summary>
        Center
    }

    #endregion

    /// <summary>
    /// 列表項的排列方向。 
    /// </summary>
    public enum LayoutDirection
    {
        Vertical,   // 垂直排列 
        Horizontal  // 水平排列 
    }

    private LayoutDirection _direction = LayoutDirection.Horizontal;
    private float _gap = 0f;
    private float _paddingLeft = 0f;
    private float _paddingTop = 0f;
    private float _paddingRight = 0f;
    private float _paddingBottom = 0f;

    // --- 新增对齐属性 ---
    private JustifyContent _justifyMain = JustifyContent.Start;
    private AlignItems _alignCross = AlignItems.Start;


    /// <summary>
    /// 佈局方向（主軸）。 
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

    /// <summary>
    /// (新) 主轴对齐方式 (Justify-Content)。 
    /// </summary>
    public JustifyContent JustifyMain
    {
        get => _justifyMain;
        set
        {
            if (_justifyMain != value)
            {
                _justifyMain = value;
                UpdateLayout();
            }
        }
    }

    /// <summary>
    /// (新) 交叉轴对齐方式 (Align-Items)。 
    /// </summary>
    public AlignItems AlignCross
    {
        get => _alignCross;
        set
        {
            if (_alignCross != value)
            {
                _alignCross = value;
                UpdateLayout();
            }
        }
    }

    #region Padding Properties (内邊距屬性)

    /// <summary>
    /// 内邊距 - 左侧。 
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
    /// </summary>
    public void SetPadding(float padding)
    {
        _paddingLeft = _paddingTop = _paddingRight = _paddingBottom = padding;
        UpdateLayout();
    }

    /// <summary>
    /// 設置内邊距 (上下相同，左右相同)。 
    /// </summary>
    public void SetPadding(float vertical, float horizontal)
    {
        _paddingTop = _paddingBottom = vertical;
        _paddingLeft = _paddingRight = horizontal;
        UpdateLayout();
    }

    /// <summary>
    /// 設置内邊距 (左、上、右、下)。 
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

    /// <summary>
    /// 添加子項並自動更新佈局。 
    /// </summary>
    public new void AddChild(DisplayObject child)
    {
        base.AddChild(child);
        UpdateLayout();
    }

    /// <summary>
    /// 批量添加子項並更新佈局。 
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
    /// </summary>
    public new void RemoveChild(DisplayObject child)
    {
        base.RemoveChild(child);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定位置插入子項並更新佈局。 
    /// </summary>
    public new void InsertChildAt(DisplayObject child, int index)
    {
        base.InsertChildAt(child, index);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定子項之前插入。 
    /// </summary>
    public new void InsertBefore(DisplayObject child, DisplayObject? beforeChild)
    {
        base.InsertBefore(child, beforeChild);
        UpdateLayout();
    }

    /// <summary>
    /// 在指定子項之後插入。 
    /// </summary>
    public new void InsertAfter(DisplayObject child, DisplayObject? afterChild)
    {
        base.InsertAfter(child, afterChild);
        UpdateLayout();
    }

    /// <summary>
    /// 清除所有子項。 
    /// </summary>
    public new void ClearChildren()
    {
        base.ClearChildren();
        UpdateLayout();
    }

    #endregion

    /// <summary>
    /// (已重写) 重新計算所有子項的位置，支持对齐。 
    /// </summary>
    public void UpdateLayout()
    {
        if (Children.Count == 0) return;

        List<DisplayObject> currentLine = [];
        // 交叉轴的起始位置 
        float crossPos = (_direction == LayoutDirection.Horizontal) ? _paddingTop : _paddingLeft;

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];

            if (child is FlowBreak)
            {
                // 遇到换行/换列符
                if (currentLine.Count > 0)
                {
                    float lineCrossSize = GetLineCrossSize(currentLine);
                    LayoutLine(currentLine, crossPos, lineCrossSize);
                    // 累加交叉轴位置
                    crossPos += lineCrossSize + (lineCrossSize > 0 ? Gap : 0);
                }
                currentLine.Clear();
            }
            else if (child.Visible)
            {
                // 可见元素，添加到当前行
                currentLine.Add(child);
            }
            // 不可见的子元素 被跳过  
        }

        // 佈局最后一行  
        if (currentLine.Count > 0)
        {
            float lineCrossSize = GetLineCrossSize(currentLine);
            LayoutLine(currentLine, crossPos, lineCrossSize);
        }
    }

    /// <summary>
    /// 辅助方法：计算一行/一列在交叉轴上的最大尺寸。 
    /// </summary>
    private float GetLineCrossSize(List<DisplayObject> lineItems)
    {
        float maxCrossSize = 0;
        bool isHorizontal = _direction == LayoutDirection.Horizontal;

        foreach (var child in lineItems)
        {
            float crossSize = isHorizontal ? child.Height : child.Width;
            if (crossSize > maxCrossSize)
            {
                maxCrossSize = crossSize;
            }
        }
        return maxCrossSize;
    }

    /// <summary>
    /// 辅助方法：佈局指定行/列中的所有元素。 
    /// </summary>
    /// <param name="lineItems">该行/列中的可见元素 </param>
    /// <param name="crossPos">该行/列的交叉轴起始位置</param>
    /// <param name="maxCrossSizeOnLine">该行/列的最大交叉轴尺寸 </param>
    private void LayoutLine(List<DisplayObject> lineItems, float crossPos, float maxCrossSizeOnLine)
    {
        if (_direction == LayoutDirection.Horizontal)
        {
            LayoutLineHorizontal(lineItems, crossPos, maxCrossSizeOnLine);
        }
        else
        {
            LayoutLineVertical(lineItems, crossPos, maxCrossSizeOnLine);
        }
    }

    /// <summary> 
    /// </summary>
    private void LayoutLineHorizontal(List<DisplayObject> lineItems, float currentY, float maxLineHeight)
    {
        float totalWidth = 0;
        int visibleItemsCount = lineItems.Count;

        // 1. 计算行指标 
        foreach (var child in lineItems)
        {
            totalWidth += child.Width;
        }
        totalWidth += Math.Max(0, visibleItemsCount - 1) * Gap;

        // 容器在主轴上的可用宽度  
        // (如果 Width 未设置，则假定无限大) 
        float layoutWidth = this.Width - PaddingLeft - PaddingRight;
        if (layoutWidth <= 0) layoutWidth = totalWidth;

        float remainingSpace = Math.Max(0, layoutWidth - totalWidth);
        float currentX = PaddingLeft;
        float spacing = Gap;

        // 2. 处理主轴对齐 
        switch (JustifyMain)
        {
            case JustifyContent.End:
                currentX += remainingSpace;
                break;
            case JustifyContent.Center:
                currentX += remainingSpace / 2;
                break;
            case JustifyContent.SpaceBetween:
                if (visibleItemsCount > 1)
                {
                    // (包含 Gap)
                    spacing = (remainingSpace / (visibleItemsCount - 1)) + Gap;
                }
                break;
        }

        // 3. 放置元素 
        foreach (var child in lineItems)
        {
            // 处理交叉轴对齐 
            float crossAxisOffset = 0;
            switch (AlignCross)
            {
                case AlignItems.End:
                    crossAxisOffset = maxLineHeight - child.Height;
                    break;
                case AlignItems.Center:
                    crossAxisOffset = (maxLineHeight - child.Height) / 2;
                    break;
                    // AlignItems.Start (默认) is 0
            }

            child.X = currentX;
            child.Y = currentY + crossAxisOffset;

            currentX += child.Width + spacing;
        }
    }

    /// <summary>
    /// 佈局垂直列 (Layout a vertical column)
    /// </summary>
    private void LayoutLineVertical(List<DisplayObject> lineItems, float currentX, float maxLineWidth)
    {
        float totalHeight = 0;
        int visibleItemsCount = lineItems.Count;

        // 1. 计算列指标 
        foreach (var child in lineItems)
        {
            totalHeight += child.Height;
        }
        totalHeight += Math.Max(0, visibleItemsCount - 1) * Gap;

        // 容器在主轴上的可用高度 
        float layoutHeight = this.Height - PaddingTop - PaddingBottom;
        if (layoutHeight <= 0) layoutHeight = totalHeight;

        float remainingSpace = Math.Max(0, layoutHeight - totalHeight);
        float currentY = PaddingTop;
        float spacing = Gap;

        // 2. 处理主轴对齐 
        switch (JustifyMain)
        {
            case JustifyContent.End:
                currentY += remainingSpace;
                break;
            case JustifyContent.Center:
                currentY += remainingSpace / 2;
                break;
            case JustifyContent.SpaceBetween:
                if (visibleItemsCount > 1)
                {
                    spacing = (remainingSpace / (visibleItemsCount - 1)) + Gap;
                }
                break;
        }

        // 3. 放置元素 
        foreach (var child in lineItems)
        {
            // 处理交叉轴对齐 
            float crossAxisOffset = 0;
            switch (AlignCross)
            {
                case AlignItems.End:
                    crossAxisOffset = maxLineWidth - child.Width;
                    break;
                case AlignItems.Center:
                    crossAxisOffset = (maxLineWidth - child.Width) / 2;
                    break;
            }

            child.X = currentX + crossAxisOffset;
            child.Y = currentY;

            currentY += child.Height + spacing;
        }
    }


    /// <summary>
    /// 加一个换行/换列符。 
    /// </summary>
    public void AddBreak()
    {
        AddChild(FlowBreak.Instance);
    }

    /// <summary>
    /// 用于获取换行符实例的静态属性。 
    /// e.g. layout.AddChild(FlowLayout.Break);
    /// </summary>
    public static FlowBreak Break => FlowBreak.Instance;

    /// <summary>
    /// 一個用於 FlowList 的標記对象，表示換行（或換列）。 
    /// 這是一個輕量級對象，它本身不可見，也不參與點擊測試。 
    /// </summary>
    public sealed class FlowBreak : DisplayObject
    {
        public override bool HitTest(PointF localPoint) => false;
        public override void Render(SharpDX.Direct2D1.RenderTarget renderTarget, ref Matrix3x2 parentTransform)
        { 
            // (此對象不渲染任何內容。)
        }

        private FlowBreak() { }

        public static FlowBreak Instance { get; } = new();
    }
}