using Pixi2D.Core;
using Pixi2D.Events;
using SharpDX.Mathematics.Interop;

using DataTable = System.Collections.Generic.IReadOnlyList<string[]>;

namespace Pixi2D.Controls;

/// <summary>
/// 高性能虚拟化表格控件
/// </summary>
public class Table : Container
{
    private DataTable? _dataSource;
    private readonly Text.Factory m_textFactory;

    // --- 配置属性 ---
    public float MaxColumnWidth { get; set; } = 200f;
    public float MinRowHeight { get; set; } = 30f;
    public float DefaultColumnWidth { get; set; } = 100f;

    // --- 外观属性 ---
    private SharpDX.Mathematics.Interop.RawColor4 _backgroundColor = new(0, 0, 0, 0);
    public SharpDX.Mathematics.Interop.RawColor4 BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; _bgDirty = true; }
    }

    private SharpDX.Mathematics.Interop.RawColor4 _borderColor = new(1, 1, 1, 1);
    public SharpDX.Mathematics.Interop.RawColor4 BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; _bgDirty = true; }
    }

    private float _borderWidth = 1f;
    public float BorderWidth
    {
        get => _borderWidth;
        set { _borderWidth = value; _bgDirty = true; }
    }

    // --- 滚动属性 ---
    private float _scrollX = 0;
    public float ScrollX
    {
        get => _scrollX;
        set { _scrollX = Math.Max(0, value); _layoutDirty = true; }
    }

    private float _scrollY = 0;
    public float ScrollY
    {
        get => _scrollY;
        set { _scrollY = Math.Max(0, value); _layoutDirty = true; }
    }

    // --- 数据源绑定 ---
    public IReadOnlyList<string[]>? DataSource
    {
        get => _dataSource;
        set
        {
            if (_dataSource == value) return;

            // UnsubscribeEvents(); // 先取消订阅旧数据源的事件

            _dataSource = value;
            _dataDirty = true;
            ScrollY = 0;
            ScrollX = 0;

            // SubscribeEvents();
        }
    }

    //private void SubscribeEvents()
    //{
    //    if (_dataSource is null) return;
    //    if (!AutoUpdate) return;
    //    _dataSource.RowChanged += OnDataRowChanged;
    //    _dataSource.RowDeleted += OnDataRowChanged;
    //    _dataSource.ColumnChanged += OnDataColChanged;
    //    _dataSource.TableCleared += OnDataTableCleared;
    //}

    //private void UnsubscribeEvents()
    //{
    //    if (_dataSource is null) return;

    //    _dataSource.RowChanged -= OnDataRowChanged;
    //    _dataSource.RowDeleted -= OnDataRowChanged;
    //    _dataSource.ColumnChanged -= OnDataColChanged;
    //    _dataSource.TableCleared -= OnDataTableCleared;
    //}

    // --- 事件处理函数 ---

    //private void OnDataColChanged(object sender, DataColumnChangeEventArgs e)
    //{
    //    _dataDirty = true;
    //}

    //private void OnDataRowChanged(object sender, DataRowChangeEventArgs e)
    //{
    //    _dataDirty = true;
    //}

    //private void OnDataTableCleared(object sender, DataTableClearEventArgs e)
    //{
    //    _dataDirty = true;
    //}

    public void NotifyDataChanged()
    {
        _dataDirty = true;
    }

    // 内部状态缓存
    private bool _dataDirty = false;
    private bool _layoutDirty = false;
    private bool _bgDirty = true;

    private float[] _colWidths = [];
    private float[] _rowHeights = [];
    private float[] _colPositions = []; // 每一列的绝对X坐标
    private float[] _rowPositions = []; // 每一行的绝对Y坐标

    private float _totalWidth = 0;
    private float _totalHeight = 0;

    // 虚拟渲染：单元格对象池
    private readonly List<TableCell> _activeCells = [];
    private readonly Stack<TableCell> _cellPool = new();

    // 内部容器与滚动条
    private readonly Graphics _background = new();
    private readonly Container _content = new();
    private readonly Graphics _hScrollBar = new();
    private readonly Graphics _hScrollThumb = new();
    private readonly Graphics _vScrollBar = new();
    private readonly Graphics _vScrollThumb = new();

    private const float ScrollBarWidth = 10f;
    private const float MinThumbSize = 20f;

    // 拖拽状态
    private bool _isDraggingH = false;
    private float _dragStartHThumbX = 0f;
    private float _dragStartScrollX = 0f;

    private bool _isDraggingV = false;
    private float _dragStartVThumbY = 0f;
    private float _dragStartScrollY = 0f;

    private Stage? _stage = null;

    private float _lastWidth = 0;
    private float _lastHeight = 0;

    public bool AutoUpdate { get; set; } = true;

    /// <summary>
    /// 无参构造：使用 <see cref="UIContext.Current"/> 默认文本工厂。
    /// </summary>
    public Table() : this(UIContext.Current.DefaultTextFactory) { }

    public Table(Text.Factory textFactory)
    {
        m_textFactory = textFactory;
        ClipContent = true; // 开启裁剪，防止溢出视窗
        Interactive = true;
        _content.Interactive = true;
        _background.Interactive = true;

        AddChild(_background);
        AddChildren(_content);

        // 初始化滚动条
        _hScrollBar.FillColor = new SharpDX.Mathematics.Interop.RawColor4(0.2f, 0.2f, 0.2f, 0.5f);
        _hScrollThumb.FillColor = new SharpDX.Mathematics.Interop.RawColor4(0.5f, 0.5f, 0.5f, 0.8f);
        _hScrollThumb.Interactive = true;

        _vScrollBar.FillColor = new SharpDX.Mathematics.Interop.RawColor4(0.2f, 0.2f, 0.2f, 0.5f);
        _vScrollThumb.FillColor = new SharpDX.Mathematics.Interop.RawColor4(0.5f, 0.5f, 0.5f, 0.8f);
        _vScrollThumb.Interactive = true;

        AddChild(_hScrollBar);
        AddChild(_hScrollThumb);
        AddChild(_vScrollBar);
        AddChild(_vScrollThumb);

        _hScrollThumb.OnMouseDown += OnHThumbMouseDown;
        _vScrollThumb.OnMouseDown += OnVThumbMouseDown;

        // 绑定垂直滚动（水平滚动通过修改 ScrollX 交由外部 ScrollBar 控制）
        OnMouseWheel += HandleMouseWheel;
    }

    private void GetViewSize(out float viewWidth, out float viewHeight)
    {
        viewWidth = Width;
        viewHeight = Height;

        if (viewWidth <= 0 || viewHeight <= 0) return;

        bool vNeeded = _totalHeight > viewHeight;
        bool hNeeded = _totalWidth > viewWidth;

        if (vNeeded) viewWidth -= ScrollBarWidth;
        if (hNeeded) viewHeight -= ScrollBarWidth;

        // Re-evaluate since subtracting width/height could trigger the other scrollbar
        if (!vNeeded && _totalHeight > viewHeight)
        {
            viewWidth -= ScrollBarWidth;
        }
        if (!hNeeded && _totalWidth > viewWidth)
        {
            viewHeight -= ScrollBarWidth;
        }

        viewWidth = Math.Max(0, viewWidth);
        viewHeight = Math.Max(0, viewHeight);
    }

    private float GetMaxScrollX()
    {
        GetViewSize(out float vw, out float vh);
        return Math.Max(0, _totalWidth - vw);
    }

    private float GetMaxScrollY()
    {
        GetViewSize(out float vw, out float vh);
        return Math.Max(0, _totalHeight - vh);
    }

    private void HandleMouseWheel(DisplayObjectEvent e)
    {
        if (e.Data is null) return;
        // 垂直鼠标滚轮事件
        ScrollY += e.Data.MouseWheelDeltaY * -0.5f;

        // 限制最大滚动范围
        ScrollY = Math.Clamp(ScrollY, 0, GetMaxScrollY());
        UpdateScrollBars();

        e.StopPropagation();
    }

    private void UpdateBackground()
    {
        _background.Clear();

        bool hasBackground = _backgroundColor.A > 0;
        bool hasBorder = _borderColor.A > 0 && _borderWidth > 0;

        // 总是填充一个背景（即使没有设置颜色也填充低透明度背景），确保提供完整的实体区域以接收和冒泡鼠标事件
        _background.FillColor = hasBackground ? _backgroundColor : new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0.01f);

        if (hasBorder)
        {
            _background.StrokeColor = _borderColor;
            _background.StrokeWidth = _borderWidth;
        }
        else
        {
            _background.StrokeWidth = 0;
        }

        _background.DrawRectangle(0, 0, Width, Height);
    }

    private void UpdateScrollBars()
    {
        GetViewSize(out float viewWidth, out float viewHeight);

        bool vNeeded = _totalHeight > viewHeight;
        bool hNeeded = _totalWidth > viewWidth;

        // 对内部单元格容器进行裁剪，留出留白及滚动条的空间
        _content.ClipContent = true;
        _content.ClipWidth = Math.Max(0, viewWidth - 5);  // 额外缩进5个像素以避免遮挡内容
        _content.ClipHeight = Math.Max(0, viewHeight - 5);

        _vScrollBar.Visible = vNeeded;
        _vScrollThumb.Visible = vNeeded;
        _hScrollBar.Visible = hNeeded;
        _hScrollThumb.Visible = hNeeded;

        // V-Scroll
        if (vNeeded)
        {
            _vScrollBar.Clear();
            _vScrollBar.DrawRoundedRectangle(0, 0, ScrollBarWidth, viewHeight, 5, 5);
            _vScrollBar.X = Width - ScrollBarWidth - 2;
            _vScrollBar.Y = 0;

            float maxScrollY = _totalHeight - viewHeight;
            float scrollableTrackY = viewHeight;
            float thumbHeight = Math.Max(MinThumbSize, (viewHeight / _totalHeight) * scrollableTrackY);

            float thumbY = (maxScrollY > 0) ? (ScrollY / maxScrollY) * (scrollableTrackY - thumbHeight) : 0;

            _vScrollThumb.Clear();
            _vScrollThumb.FillColor = _isDraggingV ? new SharpDX.Mathematics.Interop.RawColor4(0.7f, 0.7f, 0.7f, 0.8f) : new SharpDX.Mathematics.Interop.RawColor4(0.5f, 0.5f, 0.5f, 0.8f);
            _vScrollThumb.DrawRoundedRectangle(0, 0, ScrollBarWidth, thumbHeight, 5, 5);
            _vScrollThumb.X = Width - ScrollBarWidth - 2;
            _vScrollThumb.Y = thumbY;
        }

        // H-Scroll
        if (hNeeded)
        {
            _hScrollBar.Clear();
            _hScrollBar.DrawRoundedRectangle(0, 0, viewWidth, ScrollBarWidth, 5, 5);
            _hScrollBar.X = 0;
            _hScrollBar.Y = Height - ScrollBarWidth - 2;

            float maxScrollX = _totalWidth - viewWidth;
            float scrollableTrackX = viewWidth;
            float thumbWidth = Math.Max(MinThumbSize, (viewWidth / _totalWidth) * scrollableTrackX);

            float thumbX = (maxScrollX > 0) ? (ScrollX / maxScrollX) * (scrollableTrackX - thumbWidth) : 0;

            _hScrollThumb.Clear();
            _hScrollThumb.FillColor = _isDraggingH ? new SharpDX.Mathematics.Interop.RawColor4(0.7f, 0.7f, 0.7f, 0.8f) : new SharpDX.Mathematics.Interop.RawColor4(0.5f, 0.5f, 0.5f, 0.8f);
            _hScrollThumb.DrawRoundedRectangle(0, 0, thumbWidth, ScrollBarWidth, 5, 5);
            _hScrollThumb.X = thumbX;
            _hScrollThumb.Y = Height - ScrollBarWidth - 2;
        }
    }

    private void OnVThumbMouseDown(DisplayObjectEvent evt)
    {
        _isDraggingV = true;
        _dragStartVThumbY = evt.WorldPosition.Y;
        _dragStartScrollY = ScrollY;
        UpdateScrollBars();
        _stage = this.GetStage();
        if (_stage != null)
        {
            _stage.OnMouseMove += OnGlobalMouseMove;
            _stage.OnMouseUp += OnGlobalMouseUp;
        }
    }

    private void OnHThumbMouseDown(DisplayObjectEvent evt)
    {
        _isDraggingH = true;
        _dragStartHThumbX = evt.WorldPosition.X;
        _dragStartScrollX = ScrollX;
        UpdateScrollBars();
        _stage = this.GetStage();
        if (_stage != null)
        {
            _stage.OnMouseMove += OnGlobalMouseMove;
            _stage.OnMouseUp += OnGlobalMouseUp;
        }
    }

    private void OnGlobalMouseMove(DisplayObjectEvent evt)
    {
        GetViewSize(out float viewWidth, out float viewHeight);

        if (_isDraggingV)
        {
            float deltaY = evt.WorldPosition.Y - _dragStartVThumbY;
            float scrollableTrackY = viewHeight;
            float thumbHeight = Math.Max(MinThumbSize, (viewHeight / _totalHeight) * scrollableTrackY);
            float trackArea = scrollableTrackY - thumbHeight;
            if (trackArea > 0)
            {
                float scrollMax = _totalHeight - viewHeight;
                ScrollY = _dragStartScrollY + (deltaY / trackArea) * scrollMax;
                ScrollY = Math.Clamp(ScrollY, 0, scrollMax);
            }
        }

        if (_isDraggingH)
        {
            float deltaX = evt.WorldPosition.X - _dragStartHThumbX;
            float scrollableTrackX = viewWidth;
            float thumbWidth = Math.Max(MinThumbSize, (viewWidth / _totalWidth) * scrollableTrackX);
            float trackArea = scrollableTrackX - thumbWidth;
            if (trackArea > 0)
            {
                float scrollMax = _totalWidth - viewWidth;
                ScrollX = _dragStartScrollX + (deltaX / trackArea) * scrollMax;
                ScrollX = Math.Clamp(ScrollX, 0, scrollMax);
            }
        }
    }

    private void OnGlobalMouseUp(DisplayObjectEvent evt)
    {
        if (_isDraggingV || _isDraggingH)
        {
            _isDraggingV = false;
            _isDraggingH = false;
            UpdateScrollBars();
            if (_stage != null)
            {
                _stage.OnMouseMove -= OnGlobalMouseMove;
                _stage.OnMouseUp -= OnGlobalMouseUp;
                _stage = null;
            }
        }
    }

    public override void Dispose()
    {
        if (_stage != null)
        {
            _stage.OnMouseMove -= OnGlobalMouseMove;
            _stage.OnMouseUp -= OnGlobalMouseUp;
            _stage = null;
        }

        // UnsubscribeEvents(); // 确保控件销毁时不会留下强引用
        _dataSource = null;

        base.Dispose(); // 调用基类清理逻辑
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (_lastWidth != Width || _lastHeight != Height)
        {
            _lastWidth = Width;
            _lastHeight = Height;
            _layoutDirty = true;
            _bgDirty = true;
            UpdateScrollBars();
        }

        if (_bgDirty)
        {
            UpdateBackground();
            _bgDirty = false;
        }

        // 1. 数据变更时，重新计算表格的整体尺寸和行列位置
        if (_dataDirty)
        {
            CalculateDimensions();
            UpdateScrollBars();
            _layoutDirty = true; // 尺寸变了，必须要重新布局视窗内的 Cell
            _dataDirty = false;
        }

        // 2. 布局“脏”了（比如数据变了、或者发生了滚动）
        if (_layoutDirty)
        {
            // 确保滚动范围不超界
            ScrollX = Math.Clamp(ScrollX, 0, GetMaxScrollX());
            ScrollY = Math.Clamp(ScrollY, 0, GetMaxScrollY());

            UpdateVisibleCells();
            UpdateScrollBars();
            _layoutDirty = false;
        }
    }

    private void CalculateDimensions()
    {
        if (DataSource == null || DataSource.Count == 0 || DataSource[0].Length == 0)
        {
            _totalWidth = 0; _totalHeight = 0;
            return;
        }

        int colCount = DataSource[0].Length;
        int rowCount = DataSource.Count;

        _colWidths = new float[colCount];
        _colPositions = new float[colCount];
        _rowHeights = new float[rowCount];
        _rowPositions = new float[rowCount];

        // 初始化基础行高
        for (int r = 0; r < rowCount; r++)
        {
            _rowHeights[r] = MinRowHeight;
        }

        // 测量文本以计算列宽与动态行高
        float currentX = 0;
        for (int c = 0; c < colCount; c++)
        {
            float width = 0;

            for (int r = 0; r < rowCount; r++)
            {
                string text = DataSource[r][c];

                // 测量文本，留出内边距。如果文本超出 MaxColumnWidth，会根据其自动换行
                var size = m_textFactory.MeasureText(text, MaxColumnWidth);

                width = Math.Max(width, size.Width + 20);

                // 获取当前行所需的最大高度
                _rowHeights[r] = Math.Max(_rowHeights[r], size.Height + 10);
            }

            // 限制最大列宽并设置坐标
            _colWidths[c] = Math.Min(width, MaxColumnWidth + 10);
            _colPositions[c] = currentX;
            currentX += _colWidths[c];
        }
        _totalWidth = currentX;

        // 计算行坐标并累加记录总高度
        float currentY = 0;
        for (int r = 0; r < rowCount; r++)
        {
            _rowPositions[r] = currentY;
            currentY += _rowHeights[r];
        }
        _totalHeight = currentY;
    }

    private void UpdateVisibleCells()
    {
        if (DataSource == null) return;

        // 1. 回收当前所有的 Cell 入池
        foreach (var cell in _activeCells)
        {
            _content.RemoveChild(cell);
            _cellPool.Push(cell);
        }
        _activeCells.Clear();

        // 2. 二分/线性查找计算视窗内的行列范围 (View Frustum Culling)
        GetViewSize(out float viewWidth, out float viewHeight);
        int startRow = FindIndex(_rowPositions, ScrollY);
        int endRow = FindIndex(_rowPositions, ScrollY + viewHeight);

        int startCol = FindIndex(_colPositions, ScrollX);
        int endCol = FindIndex(_colPositions, ScrollX + viewWidth);

        // 防止越界
        if (endRow >= _rowHeights.Length) endRow = _rowHeights.Length - 1;
        if (endCol >= _colWidths.Length) endCol = _colWidths.Length - 1;

        // 3. 仅渲染可视范围内的 Cell
        for (int r = startRow; r <= endRow; r++)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                TableCell cell = GetOrCreateCell();

                // 判定是否是表头 
                string text = DataSource[r][c];

                // 填充数据并更新UI配置
                cell.UpdateData(text, _colWidths[c], _rowHeights[r], MaxColumnWidth);

                // 设置相对坐标（绝对坐标 - 滚动偏移量）
                cell.X = _colPositions[c] - ScrollX;
                cell.Y = _rowPositions[r] - ScrollY;

                _content.AddChild(cell);
                _activeCells.Add(cell);
            }
        }
    }

    private TableCell GetOrCreateCell()
    {
        return _cellPool.Count > 0 ? _cellPool.Pop() : new TableCell(m_textFactory.Create());
    }

    // 辅助方法：查找第一个位置小于等于 target 的索引
    private int FindIndex(float[] positions, float target)
    {
        if (positions.Length == 0) return 0;
        // 如果数据量极大（如10w行），请将其优化为真正的二分查找
        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i] > target)
                return Math.Max(0, i - 1);
        }
        return positions.Length - 1;
    }
}

/// <summary>
/// 表格单元格容器
/// </summary>
public class TableCell : Container
{
    private readonly Graphics m_background;
    private readonly Text m_text;

    /// <summary>
    /// 无参构造：使用 <see cref="UIContext.Current"/> 默认文本工厂。
    /// </summary>
    public TableCell() : this(Text.CreateDefault(string.Empty)) { }

    public TableCell(Text text)
    {
        m_background = new Graphics();
        m_text = text;
        m_text.WordWrap = true; // 启用自动换行

        AddChild(m_background);
        AddChild(m_text);
    }

    private RawColor4 m_textColor = new(1, 1, 1, 1);
    private RawColor4 m_bgColor = new(0, 0, 0, 0);
    private RawColor4 m_strokeColor = new(0.33f, 0.33f, 0.33f, 1);

    public RawColor4 BorderColor { get => m_strokeColor; set => m_strokeColor = value; }
    public RawColor4 Color { get => m_textColor; set => m_textColor = value; }
    public RawColor4 BackColor { get => m_bgColor; set => m_bgColor = value; }

    public void UpdateData(string text, float width, float height, float maxColumnWidth)
    {
        // 1. 绘制背景与边框
        m_background.Clear();
        m_background.StrokeWidth = 1;
        m_background.StrokeColor = m_strokeColor;
        m_background.FillColor = m_bgColor;
        m_background.DrawRectangle(0, 0, width, height);

        // 2. 更新文本和排版
        m_text.FillColor = m_textColor;
        m_text.Content = text;
        m_text.MaxWidth = maxColumnWidth; // 设置文本最大换行宽度

        // 文本内边距
        m_text.X = 5;
        m_text.Y = 5;
    }
}