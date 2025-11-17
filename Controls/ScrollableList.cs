using Pixi2D.Events;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 带有滚动功能的列表控件。
/// 当内容超出可视区域时，显示滚动条并支持滚动。
/// 此类已过时，请使用
/// <see cref="VirtualScrollList{T}"/>
/// </summary>
[Obsolete("ScrollableList 已过时，请使用虚拟滚动列表 VirtualScrollList")]
public class ScrollableList : Panel
{
    private readonly List _innerList;
    private readonly Graphics _scrollBar;
    private readonly Graphics _scrollThumb;

    private float _viewportHeight = 300f;
    private float _scrollPosition = 0f;
    private float _maxScrollPosition = 0f;

    private bool _isDraggingThumb = false;
    private float _dragStartY = 0f;
    private float _dragStartScrollPos = 0f;

    private const float ScrollBarWidth = 10f;
    private const float MinThumbHeight = 20f;

    /// <summary>
    /// 创建一个新的可滚动列表。
    /// </summary>
    public ScrollableList(float width = 200f, float height = 300f) : base(width, height)
    {
        ContentContainer.Interactive = true;
        _viewportHeight = height;

        // 创建内部列表
        _innerList = new List
        {
            Direction = List.LayoutDirection.Vertical,
            ItemSpacing = 5f
        };

        // 添加到内容容器
        ContentContainer.AddChild(_innerList);

        // 创建滚动条背景
        _scrollBar = new Graphics
        {
            X = width - ScrollBarWidth - 5,
            Y = 5,
            Visible = false
        };
        _scrollBar.FillColor = new RawColor4(0.2f, 0.2f, 0.2f, 0.5f);
        _scrollBar.DrawRoundedRectangle(0, 0, ScrollBarWidth, _viewportHeight - 10, 5, 5);
        ContentContainer.AddChild(_scrollBar);

        // 创建滚动条滑块
        _scrollThumb = new Graphics
        {
            X = width - ScrollBarWidth - 5,
            Y = 5,
            Visible = false,
            Interactive = true
        };
        _scrollThumb.FillColor = new RawColor4(0.5f, 0.5f, 0.5f, 0.8f);
        UpdateScrollThumb();
        ContentContainer.AddChild(_scrollThumb);

        // 注册滑块事件
        _scrollThumb.OnMouseDown += OnThumbMouseDown;
        _scrollThumb.OnMouseMove += OnThumbMouseMove;
        _scrollThumb.OnMouseUp += OnThumbMouseUp;
        this.OnMouseWheel += HandleMouseWheel;

        Interactive = true;
    }

    private void HandleMouseWheel(DisplayObjectEvent @event)
    {
        float deltaY = @event.Data?.MouseWheelDeltaY ?? 0f;
        if (deltaY != 0)
            ScrollPosition -= deltaY; // 向上滚动时，deltaY 为正值
        @event.StopPropagation();
    }

    /// <summary>
    /// 内部列表 (用于添加项目)。
    /// </summary>
    public List InnerList => _innerList;

    /// <summary>
    /// 可视区域高度。
    /// </summary>
    public float ViewportHeight
    {
        get => _viewportHeight;
        set
        {
            _viewportHeight = value;
            UpdateScrollBar();
        }
    }

    /// <summary>
    /// 当前滚动位置 (0 到 MaxScrollPosition)。
    /// </summary>
    public float ScrollPosition
    {
        get => _scrollPosition;
        set
        {
            _scrollPosition = Math.Clamp(value, 0f, _maxScrollPosition);
            UpdateContentPosition();
            UpdateScrollThumb();
        }
    }

    /// <summary>
    /// 最大滚动位置。
    /// </summary>
    public float MaxScrollPosition => _maxScrollPosition;

    /// <summary>
    /// 添加子项到列表。
    /// </summary>
    public void AddItem(DisplayObject item)
    {
        _innerList.AddChild(item);
        UpdateScrollBar();
    }

    /// <summary>
    /// 批量添加子项到列表。
    /// </summary>
    public void AddItems(params DisplayObject[] items)
    {
        _innerList.AddChildren(items);
        UpdateScrollBar();
    }

    /// <summary>
    /// 从列表中移除子项。
    /// </summary>
    public void RemoveItem(DisplayObject item)
    {
        _innerList.RemoveChild(item);
        UpdateScrollBar();
    }

    /// <summary>
    /// 清除列表中的所有项目。
    /// </summary>
    public void ClearItems()
    {
        _innerList.ClearChildren();
        UpdateScrollBar();
        ScrollPosition = 0f;
    }

    /// <summary>
    /// 向上滚动指定距离。
    /// </summary>
    public void ScrollUp(float amount = 30f)
    {
        ScrollPosition -= amount;
    }

    /// <summary>
    /// 向下滚动指定距离。
    /// </summary>
    public void ScrollDown(float amount = 30f)
    {
        ScrollPosition += amount;
    }

    /// <summary>
    /// 滚动到顶部。
    /// </summary>
    public void ScrollToTop()
    {
        ScrollPosition = 0f;
    }

    /// <summary>
    /// 滚动到底部。
    /// </summary>
    public void ScrollToBottom()
    {
        ScrollPosition = _maxScrollPosition;
    }

    /// <summary>
    /// 滚动到指定索引的项。
    /// </summary>
    /// <param name="index">要滚动到的项的索引。</param>
    public void ScrollTo(int index)
    {
        var position = _innerList.GetItemPosition(index);
        if (position.HasValue)
        {
            ScrollPosition = position.Value;
        }
    }

    /// <summary>
    /// 滚动到指定的显示对象。
    /// </summary>
    /// <param name="item">要滚动到的显示对象。</param>
    public void ScrollTo(DisplayObject item)
    {
        int index = _innerList.GetItemIndex(item);
        if (index >= 0)
            ScrollTo(index);
    }

    /// <summary>
    /// 滚动到开始位置 (等同于 ScrollToTop)。
    /// </summary>
    public void ScrollToBegin()
    {
        ScrollToTop();
    }

    /// <summary>
    /// 滚动到结束位置 (等同于 ScrollToBottom)。
    /// </summary>
    public void ScrollToEnd()
    {
        ScrollToBottom();
    }

    /// <summary>
    /// 更新滚动条的可见性和尺寸。
    /// </summary>
    private void UpdateScrollBar()
    {
        var (_, contentHeight) = _innerList.GetContentSize();

        _maxScrollPosition = Math.Max(0f, contentHeight - _viewportHeight);

        bool needsScrollBar = contentHeight > _viewportHeight;
        _scrollBar.Visible = needsScrollBar;
        _scrollThumb.Visible = needsScrollBar;

        if (needsScrollBar)
        {
            UpdateScrollThumb();
        }

        // 确保滚动位置在有效范围内
        if (_scrollPosition > _maxScrollPosition)
        {
            _scrollPosition = _maxScrollPosition;
            UpdateContentPosition();
        }
    }

    /// <summary>
    /// 更新滚动条滑块的位置和大小。
    /// </summary>
    private void UpdateScrollThumb()
    {
        if (_maxScrollPosition <= 0) return;

        var (_, contentHeight) = _innerList.GetContentSize();

        // 计算滑块高度 (基于可见内容的比例)
        float thumbHeight = Math.Max(MinThumbHeight,
            (_viewportHeight / contentHeight) * (_viewportHeight - 10));

        // 计算滑块位置 (基于滚动位置)
        float scrollBarHeight = _viewportHeight - 10;
        float scrollableThumbArea = scrollBarHeight - thumbHeight;
        float thumbY = 5 + (scrollableThumbArea * (_scrollPosition / _maxScrollPosition));

        // 更新滑块图形
        _scrollThumb.Clear();
        _scrollThumb.FillColor = new RawColor4(0.5f, 0.5f, 0.5f, 0.8f);
        _scrollThumb.DrawRoundedRectangle(0, 0, ScrollBarWidth, thumbHeight, 5, 5);
        _scrollThumb.Y = thumbY;
    }

    /// <summary>
    /// 更新内容位置 (基于滚动位置)。
    /// </summary>
    private void UpdateContentPosition()
    {
        _innerList.Y = -_scrollPosition;
    }

    private void OnThumbMouseDown(Events.DisplayObjectEvent evt)
    {
        _isDraggingThumb = true;
        _dragStartY = evt.WorldPosition.Y;
        _dragStartScrollPos = _scrollPosition;
    }

    private void OnThumbMouseMove(Events.DisplayObjectEvent evt)
    {
        if (!_isDraggingThumb) return;

        float deltaY = evt.WorldPosition.Y - _dragStartY;

        // 计算滚动条高度和可滚动区域
        float scrollBarHeight = _viewportHeight - 10;
        var (_, contentHeight) = _innerList.GetContentSize();
        float thumbHeight = Math.Max(MinThumbHeight,
            (_viewportHeight / contentHeight) * scrollBarHeight);
        float scrollableThumbArea = scrollBarHeight - thumbHeight;

        if (scrollableThumbArea > 0)
        {
            // 将滑块移动距离转换为内容滚动距离
            float scrollDelta = (deltaY / scrollableThumbArea) * _maxScrollPosition;
            ScrollPosition = _dragStartScrollPos + scrollDelta;
        }
    }

    private void OnThumbMouseUp(Events.DisplayObjectEvent evt)
    {
        _isDraggingThumb = false;
    }

    public override void Dispose()
    {
        _scrollThumb.OnMouseDown -= OnThumbMouseDown;
        _scrollThumb.OnMouseMove -= OnThumbMouseMove;
        _scrollThumb.OnMouseUp -= OnThumbMouseUp;
        base.Dispose();
    }
}
