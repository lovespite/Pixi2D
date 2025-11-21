using Pixi2D.Core;
using Pixi2D.Events;
using SharpDX;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 虚拟滚动列表控件。
/// 只渲染可见区域内的项目，适用于大数据量列表，可大幅提升性能。
/// </summary>
/// <typeparam name="T">列表项数据的类型。</typeparam>
public class VirtualScrollList<T> : Panel
{
    private readonly List<T> _dataSource = [];
    private readonly Dictionary<int, DisplayObject> _visibleItems = [];
    private readonly Graphics _scrollBar;
    private readonly Graphics _scrollThumb;

    private float _viewportHeight = 300f;
    private float _itemHeight = 40f;
    private float _scrollPosition = 0f;
    private float _maxScrollPosition = 0f;
    private int _scrollCount = 1;

    private bool _isDraggingThumb = false;
    private float _dragStartY = 0f;
    private float _dragStartScrollPos = 0f;
    private Stage? _stage = null;

    private const float ScrollBarWidth = 10f;
    private const float MinThumbHeight = 20f;

    /// <summary>
    /// 创建列表项的委托。
    /// </summary>
    public Func<T, int, DisplayObject>? ItemRenderer { get; set; }

    /// <summary>
    /// 当项目被点击时触发。
    /// </summary>
    public event Action<T, int>? OnItemClick;

    /// <summary>
    /// 创建一个新的虚拟滚动列表。
    /// </summary>
    /// <param name="width">列表宽度。</param>
    /// <param name="height">列表高度（可视区域高度）。</param>
    /// <param name="itemHeight">每个列表项的高度。</param>
    public VirtualScrollList(float width = 200f, float height = 300f, float itemHeight = 40f) : base(width, height)
    {
        ContentContainer.Interactive = true;
        _viewportHeight = height;
        _itemHeight = itemHeight;

        // 启用内容裁剪（内容区域不包含滚动条）
        ContentContainer.ClipContent = true;
        ContentContainer.ClipWidth = width - ScrollBarWidth - 10;
        ContentContainer.ClipHeight = height;

        // 创建滚动条背景（添加到主容器，不受内容裁剪影响）
        _scrollBar = new Graphics
        {
            X = width - ScrollBarWidth - 5,
            Y = 5,
            Visible = false,
            FillColor = new(0.2f, 0.2f, 0.2f, 0.5f)
        };
        _scrollBar.DrawRoundedRectangle(0, 0, ScrollBarWidth, _viewportHeight - 10, 5, 5);
        base.AddChild(_scrollBar);

        // 创建滚动条滑块（添加到主容器，不受内容裁剪影响）
        _scrollThumb = new Graphics
        {
            X = width - ScrollBarWidth - 5,
            Y = 5,
            Visible = false,
            Interactive = true,
            FillColor = new(0.5f, 0.5f, 0.5f, 0.8f)
        };
        UpdateScrollThumb();
        base.AddChild(_scrollThumb);

        // 注册滑块事件
        _scrollThumb.OnMouseDown += OnThumbMouseDown;
        this.OnMouseWheel += HandleMouseWheel;

        Interactive = true;
    }

    public VirtualScrollList(SizeF itemSize)
        : this(itemSize.Width, itemSize.Height, itemSize.Height)
    {
    }

    /// <summary>
    /// 每个列表项的高度。
    /// </summary>
    public float ItemHeight
    {
        get => _itemHeight;
        set
        {
            if (_itemHeight != value)
            {
                _itemHeight = value;
                UpdateScrollBar();
                UpdateVisibleItems();
            }
        }
    }

    /// <summary>
    /// 可视区域高度。
    /// </summary>
    public float ViewportHeight
    {
        get => _viewportHeight;
        set
        {
            if (_viewportHeight != value)
            {
                _viewportHeight = Math.Max(50, value);
                UpdateScrollBar();
                UpdateVisibleItems();
            }
        }
    }

    /// <summary>
    /// 当前滚动位置。
    /// </summary>
    public float ScrollPosition
    {
        get => _scrollPosition;
        set
        {
            float newPos = Math.Clamp(value, 0f, _maxScrollPosition);
            if (_scrollPosition != newPos)
            {
                _scrollPosition = newPos;
                UpdateVisibleItems();
                UpdateScrollThumb();
            }
        }
    }

    /// <summary>
    /// 每次滚动的项目数量（滚动视窗的大小）。
    /// </summary>
    public int ScrollCount
    {
        get => _scrollCount;
        set => _scrollCount = Math.Max(1, value);
    }

    /// <summary>
    /// 数据源的项目数量。
    /// </summary>
    public int ItemCount => _dataSource.Count;

    /// <summary>
    /// 设置数据源。
    /// </summary>
    /// <param name="data">数据集合。</param>
    public void SetData(IEnumerable<T> data)
    {
        _dataSource.Clear();
        _dataSource.AddRange(data);
        // 数据源彻底改变，必须清除可见项缓存，否则会显示旧数据
        ClearVisibleItems();
        ScrollPosition = 0f;
        UpdateScrollBar();
        UpdateVisibleItems();
    }

    /// <summary>
    /// 添加单个数据项。
    /// </summary>
    public void AddItem(T item)
    {
        _dataSource.Add(item);
        UpdateScrollBar();
        UpdateVisibleItems();
    }

    /// <summary>
    /// 批量添加数据项。
    /// </summary>
    public void AddItems(IEnumerable<T> items)
    {
        _dataSource.AddRange(items);
        UpdateScrollBar();
        UpdateVisibleItems();
    }

    /// <summary>
    /// 在指定索引处插入数据项。
    /// </summary>
    public void InsertItem(int index, T item)
    {
        if (index < 0 || index > _dataSource.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _dataSource.Insert(index, item);
        // 插入会导致后续索引偏移，必须清除缓存以防复用错误的数据视图
        ClearVisibleItems();
        UpdateScrollBar();
        UpdateVisibleItems();
    }

    /// <summary>
    /// 移除指定索引的数据项。
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _dataSource.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _dataSource.RemoveAt(index);
        // 移除会导致后续索引偏移，必须清除缓存
        ClearVisibleItems();
        UpdateScrollBar();
        UpdateVisibleItems();
    }

    /// <summary>
    /// 移除指定的数据项。
    /// </summary>
    public bool RemoveItem(T item)
    {
        bool removed = _dataSource.Remove(item);
        if (removed)
        {
            // 移除会导致后续索引偏移，必须清除缓存
            ClearVisibleItems();
            UpdateScrollBar();
            UpdateVisibleItems();
        }
        return removed;
    }

    /// <summary>
    /// 更新指定索引的数据项。
    /// </summary>
    public void UpdateItem(int index, T item)
    {
        if (index < 0 || index >= _dataSource.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _dataSource[index] = item;

        // 针对特定索引清除缓存，强制下次更新时重建
        if (_visibleItems.TryGetValue(index, out var displayObject))
        {
            ContentContainer.RemoveChild(displayObject);
            displayObject.Dispose();
            _visibleItems.Remove(index);
        }
        UpdateVisibleItems();
    }

    /// <summary>
    /// 清除所有数据项。
    /// </summary>
    public void Clear()
    {
        _dataSource.Clear();
        ClearVisibleItems();
        ScrollPosition = 0f;
        UpdateScrollBar();
    }

    /// <summary>
    /// 获取指定索引的数据项。
    /// </summary>
    public T? GetItem(int index)
    {
        if (index < 0 || index >= _dataSource.Count)
            return default;
        return _dataSource[index];
    }

    /// <summary>
    /// 滚动到指定索引的项。
    /// </summary>
    public void ScrollToIndex(int index)
    {
        if (index < 0 || index >= _dataSource.Count)
            return;

        float targetPosition = index * _itemHeight;
        ScrollPosition = targetPosition;
    }

    /// <summary>
    /// Scrolls the view to bring the specified item into view, if it exists in the data source.
    /// </summary>
    /// <param name="item">The item to scroll into view. If the item is not found in the data source, no action is taken.</param>
    public void ScrollToItem(T item)
    {
        int index = _dataSource.IndexOf(item);
        if (index >= 0)
        {
            ScrollToIndex(index);
        }
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
    /// 刷新可见项的渲染（强制重新创建）。
    /// </summary>
    public void Refresh()
    {
        // 强制清除缓存
        ClearVisibleItems();
        UpdateVisibleItems();
    }

    private void HandleMouseWheel(DisplayObjectEvent @event)
    {
        float deltaY = @event.Data?.MouseWheelDeltaY ?? 0f;
        if (deltaY != 0)
        {
            // 根据 ScrollCount 计算滚动距离
            float scrollAmount = _scrollCount * _itemHeight;
            ScrollPosition -= Math.Sign(deltaY) * scrollAmount;
        }
        @event.StopPropagation();
    }

    private void UpdateScrollBar()
    {
        float contentHeight = _dataSource.Count * _itemHeight;
        _maxScrollPosition = Math.Max(0f, contentHeight - _viewportHeight);

        bool needsScrollBar = contentHeight > _viewportHeight;
        _scrollBar.Visible = needsScrollBar;
        _scrollThumb.Visible = needsScrollBar;

        _scrollBar.Clear();
        _scrollBar.DrawRoundedRectangle(0, 0, ScrollBarWidth, _viewportHeight - 10, 5, 5);

        if (needsScrollBar)
        {
            UpdateScrollThumb();
        }

        // 确保滚动位置在有效范围内
        if (_scrollPosition > _maxScrollPosition)
        {
            _scrollPosition = _maxScrollPosition;
        }
    }

    private void UpdateScrollThumb()
    {
        if (_maxScrollPosition <= 0) return;

        float contentHeight = _dataSource.Count * _itemHeight;

        // 计算滑块高度
        float thumbHeight = Math.Max(MinThumbHeight,
            (_viewportHeight / contentHeight) * (_viewportHeight - 10));

        // 计算滑块位置
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
    /// 核心方法：更新可见项目。
    /// 只创建和渲染当前可视区域内的项目。
    /// </summary>
    private void UpdateVisibleItems()
    {
        if (ItemRenderer is null || _dataSource.Count == 0)
        {
            ClearVisibleItems();
            return;
        }

        // 计算可见范围
        int firstVisibleIndex = (int)Math.Floor(_scrollPosition / _itemHeight);
        int lastVisibleIndex = (int)Math.Floor((_scrollPosition + _viewportHeight) / _itemHeight);

        // 确保索引在有效范围内
        firstVisibleIndex = Math.Max(0, firstVisibleIndex);
        lastVisibleIndex = Math.Min(_dataSource.Count - 1, lastVisibleIndex);

        // 添加缓冲区（预渲染前后各一项）
        int bufferSize = 1;
        firstVisibleIndex = Math.Max(0, firstVisibleIndex - bufferSize);
        lastVisibleIndex = Math.Min(_dataSource.Count - 1, lastVisibleIndex + bufferSize);

        // 1. 移除不再可见的项目
        // 使用 ToList 创建副本以便在遍历时修改字典
        var indicesToRemove = _visibleItems.Keys
            .Where(index => index < firstVisibleIndex || index > lastVisibleIndex)
            .ToList();

        foreach (var index in indicesToRemove)
        {
            if (_visibleItems.TryGetValue(index, out var item))
            {
                ContentContainer.RemoveChild(item);
                item.Dispose();
                _visibleItems.Remove(index);
            }
        }

        // 2. 创建或更新可见项目
        for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
        {
            // 优化：如果该索引的项目已存在，仅更新位置，不再销毁重建！
            if (_visibleItems.TryGetValue(i, out var existingItem))
            {
                existingItem.Y = i * _itemHeight - _scrollPosition;
                continue;
            }

            // 只有不存在时，才创建新项
            var data = _dataSource[i];
            var newItem = ItemRenderer(data, i);

            // 设置项目位置
            newItem.Y = i * _itemHeight - _scrollPosition;

            // 添加点击事件
            // 注意：闭包捕获变量
            int capturedIndex = i;
            T capturedData = data;
            newItem.OnClick += (evt) =>
            {
                OnItemClick?.Invoke(capturedData, capturedIndex);
            };

            // 添加到内容容器
            ContentContainer.AddChild(newItem);

            _visibleItems[i] = newItem;
        }
    }

    private void ClearVisibleItems()
    {
        foreach (var kvp in _visibleItems)
        {
            ContentContainer.RemoveChild(kvp.Value);
            kvp.Value.Dispose();
        }
        _visibleItems.Clear();
    }

    private void OnThumbMouseDown(DisplayObjectEvent evt)
    {
        _isDraggingThumb = true;
        _dragStartY = evt.WorldPosition.Y;
        _dragStartScrollPos = _scrollPosition;

        // 获取 Stage 并注册全局鼠标事件
        _stage = GetStage();
        if (_stage is not null)
        {
            _stage.OnMouseMove += OnGlobalMouseMove;
            _stage.OnMouseUp += OnGlobalMouseUp;
        }
    }

    private void OnGlobalMouseMove(DisplayObjectEvent evt)
    {
        if (!_isDraggingThumb) return;

        float deltaY = evt.WorldPosition.Y - _dragStartY;

        // 计算滚动条高度和可滚动区域
        float scrollBarHeight = _viewportHeight - 10;
        float contentHeight = _dataSource.Count * _itemHeight;
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

    private void OnGlobalMouseUp(DisplayObjectEvent evt)
    {
        if (_isDraggingThumb)
        {
            _isDraggingThumb = false;

            // 取消注册全局鼠标事件
            if (_stage is not null)
            {
                _stage.OnMouseMove -= OnGlobalMouseMove;
                _stage.OnMouseUp -= OnGlobalMouseUp;
                _stage = null;
            }
        }
    }

    public override void Dispose()
    {
        _scrollThumb.OnMouseDown -= OnThumbMouseDown;
        this.OnMouseWheel -= HandleMouseWheel;

        // 清理全局事件（如果还在拖拽中）
        if (_stage is not null)
        {
            _stage.OnMouseMove -= OnGlobalMouseMove;
            _stage.OnMouseUp -= OnGlobalMouseUp;
            _stage = null;
        }

        ClearVisibleItems();
        base.Dispose();
    }
}
