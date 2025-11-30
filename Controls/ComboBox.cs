using Pixi2D.Components;
using Pixi2D.Core;
using Pixi2D.Events;
using Pixi2D.Extensions;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Controls;

/// <summary>
/// 一个简单的下拉选择框控件 (ComboBox)。
/// <para>
/// 特性：
/// 1. 支持任何实现 ToString() 方法的对象作为选项。
/// 2. 兼容纯字符串添加 (自动封装)。
/// 3. 下拉菜单挂载在 Stage 上，显示在最上层。
/// </para>
/// </summary>
public class ComboBox : Container
{
    // --- UI 组件 ---
    private readonly Graphics _header;
    private readonly Text _selectedLabel;
    private readonly Graphics _arrow;

    // --- 下拉浮动层 ---
    private readonly Graphics _overlay; // 全屏透明遮罩，用于检测点击外部
    private readonly VirtualScrollList<object> _optionsList;

    // --- 数据 ---
    private readonly Text.Factory _textFactory;

    // 存储object
    private readonly List<object> _items = [];

    private bool _isOpen = false;
    private int _selectedIndex = -1;
    private float _width;
    private float _height;
    private float _itemHeight = 28f;
    private string _placeholder = "Select...";

    public override float Width
    {
        get => _width;
        set => _ = value; // Not supported yet ...
    }

    public override float Height
    {
        get => _height;
        set => _ = value; // Not supported yet ...
    }

    /// <summary>
    /// 当选中的项发生改变时触发。 
    /// </summary>
    public event Action<ComboBox, object, int>? OnSelectionChanged;

    /// <summary>
    /// 简单的字符串包装类，用于兼容 AddItem(string)
    /// </summary>
    private class SimpleStringDisplay(string text)
    {
        public string Text { get; } = text;
        public override string ToString() => Text;
    }

    /// <summary>
    /// 创建一个新的 ComboBox。
    /// </summary>
    /// <param name="textFactory">文本工厂，用于创建标签。</param>
    /// <param name="width">控件宽度。</param>
    /// <param name="height">控件高度。</param>
    public ComboBox(Text.Factory textFactory, float itemHeight, float width = 150f, float height = 30f)
    {
        _textFactory = textFactory;
        _width = width;
        _height = height;
        _itemHeight = itemHeight;

        Interactive = true; // 自身可交互

        // 1. 创建标题栏背景 (Header)
        _header = new Graphics
        {
            Interactive = true, // 响应点击
            FillColor = new RawColor4(0.2f, 0.2f, 0.2f, 1f),
            StrokeColor = new RawColor4(0.5f, 0.5f, 0.5f, 1f),
            StrokeWidth = 1f
        };
        _header.DrawRoundedRectangle(0, 0, width, height, 4, 4);
        _header.OnClick += (e) => Toggle();
        _header.OnMouseOver += (e) => _header.FillColor = new RawColor4(0.3f, 0.3f, 0.3f, 1f);
        _header.OnMouseOut += (e) => _header.FillColor = new RawColor4(0.2f, 0.2f, 0.2f, 1f);
        AddChild(_header);

        // 2. 创建选中显示文本
        _selectedLabel = textFactory.Create(_placeholder, 14, Color.White);
        _selectedLabel.X = 8;
        _selectedLabel.Y = (height - _selectedLabel.FontSize) / 2f - 2;
        _selectedLabel.MaxWidth = width - 30; // 留出箭头空间
        _selectedLabel.WordWrap = false;      // 单行
        AddChild(_selectedLabel);

        // 3. 创建下拉箭头 (简单的 V 形)
        _arrow = new Graphics
        {
            X = width - 18,
            Y = height / 2 - 3,
            Interactive = false
        };
        _arrow.DrawPolygon([
            new PointF(0, 0),
            new PointF(5, 5),
            new PointF(10, 0)
        ]);
        _arrow.FillColor = new RawColor4(0.8f, 0.8f, 0.8f, 1f);
        _arrow.SetAnchorCenter();
        AddChild(_arrow);

        // --- 浮动层组件 ---

        // 4. 初始化遮罩层
        _overlay = new Graphics
        {
            Interactive = true,
            Visible = false,
            FillColor = new RawColor4(0, 0, 0, 0.0f)
        };
        _overlay.OnClick += (e) => Close();

        // 5. 初始化下拉面板 
        _optionsList = new VirtualScrollList<object>(width, _itemHeight)
        {
            ItemRenderer = CreateOptionControl,
            BackgroundColor = new RawColor4(0.25f, 0.25f, 0.25f, 1f),
            BorderColor = new RawColor4(0.4f, 0.4f, 0.4f, 1f),
            BorderWidth = 2,
            ItemHeight = _itemHeight,
        };
    }

    #region Public Properties

    /// <summary>
    /// 获取当前选中的对象。如果未选中，则为 null。
    /// </summary>
    public object? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    /// <summary>
    /// 获取当前选中的文本。如果未选中，返回空字符串。
    /// </summary>
    public string SelectedText => SelectedItem?.ToString() ?? string.Empty;

    /// <summary>
    /// 获取当前选中的索引，未选中为 -1。
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Select(value);
    }

    /// <summary>
    /// 未选中时的占位符文本。
    /// </summary>
    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            if (_selectedIndex == -1)
            {
                _selectedLabel.Content = value;
                _selectedLabel.FillColor = new RawColor4(0.7f, 0.7f, 0.7f, 1f);
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// 添加一个选项。
    /// </summary>
    public void AddItem(object item)
    {
        _items.Add(item);
        _optionsList.AddItem(item);
        UpdateDropdownSize();
    }

    /// <summary>
    /// 添加一个字符串选项 (自动封装)。
    /// </summary>
    public void AddItem(string text)
    {
        AddItem(new SimpleStringDisplay(text));
    }

    /// <summary>
    /// 批量设置选项。
    /// </summary>
    public void SetItems(IEnumerable<object> items)
    {
        ClearItemsInternal();
        _optionsList.AddItems(items);
        UpdateDropdownSize();
    }


    private void ClearItemsInternal()
    {
        _items.Clear();
        _optionsList.Clear();
        _selectedIndex = -1;
        _selectedLabel.Content = _placeholder;
        _selectedLabel.FillColor = new RawColor4(0.7f, 0.7f, 0.7f, 1f);
    }

    /// <summary>
    /// 选中指定索引的项。
    /// </summary>
    public void Select(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            _selectedIndex = -1;
            _selectedLabel.Content = _placeholder;
            _selectedLabel.FillColor = new RawColor4(0.7f, 0.7f, 0.7f, 1f);
        }
        else
        {
            _selectedIndex = index;
            var item = _items[index];
            _selectedLabel.Content = item.ToString() ?? string.Empty;
            _selectedLabel.FillColor = Color.White.ToRawColor4();
            OnSelectionChanged?.Invoke(this, item, _selectedIndex);
        }
    }

    /// <summary>
    /// 切换下拉状态。
    /// </summary>
    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    /// <summary>
    /// 打开下拉菜单。
    /// </summary>
    public void Open()
    {
        if (_isOpen) return;

        var stage = GetStage();
        if (stage == null) return;

        _isOpen = true;

        // 添加遮罩
        _overlay.Clear();
        _overlay.DrawRectangle(0, 0, stage.Width, stage.Height);
        _overlay.Visible = true;
        stage.AddChild(_overlay);

        // 更新位置
        UpdateDropdownPosition();

        // 显示面板
        _optionsList.Visible = true;
        stage.AddChild(_optionsList);

        // 旋转箭头
        _arrow.Rotation = (float)Math.PI;
    }

    /// <summary>
    /// 关闭下拉菜单。
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _overlay.Visible = false;
        _optionsList.Visible = false;

        _overlay.Parent?.RemoveChild(_overlay);
        _optionsList.Parent?.RemoveChild(_optionsList);

        _arrow.Rotation = 0;
    }

    #endregion

    #region Internal Logic

    /// <summary>
    /// 创建单个选项的显示对象。
    /// </summary>
    private ComboBoxItem CreateOptionControl(object item, int index)
    {
        var container = new ComboBoxItem(_textFactory, item, _itemHeight)
        {
            Width = _width - 16,
            Height = 28f,
            LabelTextColor = Color.White,
            HoverBackgroundColor = Color.FromArgb(0x33, 0x66, 0x99),
            NormalBackgroundColor = Color.FromArgb(0, 0, 0, 0)
        };
        container.OnItemClick += (ci) =>
        {
            Select(index);
            Close();
        };

        return container;
    }

    private void UpdateDropdownSize()
    {

        float totalHeight = _items.Count * _itemHeight;
        float maxHeight = 300f;

        _optionsList.Height = Math.Clamp(totalHeight, _itemHeight, maxHeight) + 2;
        _optionsList.ViewportHeight = _optionsList.Height;
        _optionsList.Refresh();
    }

    private void UpdateDropdownPosition()
    {
        if (!_isOpen) return;
        var stage = GetStage();
        if (stage == null) return;

        var worldTransform = this.GetWorldTransform();
        var worldPos = Vector2.Transform(new Vector2(0, _height), worldTransform);

        _optionsList.X = worldPos.X;
        _optionsList.Y = worldPos.Y;

        if (_optionsList.Y + _optionsList.Height > stage.Height)
        {
            var worldPosTop = Vector2.Transform(new Vector2(0, 0), worldTransform);
            _optionsList.Y = worldPosTop.Y - _optionsList.Height;
        }
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);
        if (_isOpen && _optionsList.Parent != null)
        {
            UpdateDropdownPosition();
        }
    }

    public override void Dispose()
    {
        Close();
        _header.Dispose();
        _overlay.Dispose();
        _optionsList.Dispose();
        _arrow.Dispose();
        base.Dispose();
    }

    #endregion
}

internal class ComboBoxItem : Container
{
    private readonly Text _label;
    private readonly Graphics _background;
    private float _itemHeight = 28f;
    private float _itemWidth = 150f;
    private bool _dirty = false;
    private Color _labelTextColor = Color.White;
    private Color _hoverBackgroundColor = Color.FromArgb(0x33, 0x66, 0x99);
    private Color _normalBackgroundColor = Color.FromArgb(0, 0, 0, 0);

    public Color LabelTextColor
    {
        get => _labelTextColor;
        set
        {
            _labelTextColor = value;
            _dirty = true;
        }
    }
    public Color HoverBackgroundColor
    {
        get => _hoverBackgroundColor;
        set
        {
            _hoverBackgroundColor = value;
            _dirty = true;
        }
    }
    public Color NormalBackgroundColor
    {
        get => _normalBackgroundColor;
        set
        {
            _normalBackgroundColor = value;
            _dirty = true;
        }
    }
    public override float Height
    {
        get => _itemHeight;
        set
        {
            _itemHeight = value;
            _dirty = true;
        }
    }
    public override float Width
    {
        get => _itemWidth;
        set
        {
            _itemWidth = value;
            _dirty = true;
        }
    }

    public object ItemObject { get; }

    public event Action<ComboBoxItem>? OnItemClick;

    public ComboBoxItem(Text.Factory textFactory, object itemObject, float itemHeight = 28f)
    {
        ItemObject = itemObject;
        _itemHeight = itemHeight;
        _label = textFactory.Create(itemObject.ToString() ?? string.Empty, 14, LabelTextColor);
        _label.X = 8;
        _label.Y = (_itemHeight - 14) / 2f - 2;
        _background = new Graphics
        {
            Interactive = true,
            FillColor = NormalBackgroundColor.ToRawColor4()
        };
        _background.OnMouseOver += (e) => _background.FillColor = HoverBackgroundColor.ToRawColor4();
        _background.OnMouseOut += (e) => _background.FillColor = NormalBackgroundColor.ToRawColor4();
        _background.OnClick += (e) => OnItemClick?.Invoke(this);
        AddChildren(_background, _label);
        UpdateLayout();
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);
        if (!_dirty) return;

        UpdateLayout();
    }

    private void UpdateLayout()
    {
        _dirty = false;
        _background.Clear();
        _background.FillColor = NormalBackgroundColor.ToRawColor4();
        _background.StrokeColor = NormalBackgroundColor.ToRawColor4();
        _background.DrawRectangle(0, 0, _itemWidth, _itemHeight);
    }
}