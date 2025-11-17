using Pixi2D.Events;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Controls;

/// <summary>
/// 列表项容器，通常包含背景和内容。
/// 提供选中状态、悬停效果等功能。
/// </summary>
public class ListItem : Container
{
    private readonly Graphics _background;
    private readonly Container _contentContainer;

    private RawColor4 _normalColor = new(0.0f, 0.0f, 0.0f, 0f);     // 普通状态颜色
    private RawColor4 _hoverColor = new(0.3f, 0.3f, 0.3f, 1.0f);      // 悬停状态颜色

    private bool _isHovered = false;

    private float _itemWidth = 200f;
    private float _itemHeight = 40f;

    public object? Data { get; set; }

    /// <summary>
    /// 创建一个新的列表项。
    /// </summary>
    public ListItem(float width = 200f, float height = 40f)
    {
        _itemWidth = width;
        _itemHeight = height;
        ClipContent = true;
        ClipWidth = _itemWidth;
        ClipHeight = _itemHeight;

        // 创建背景
        _background = new Graphics
        {
            Interactive = true
        };
        _background.FillColor = _normalColor;
        _background.DrawRectangle(0, 0, _itemWidth, _itemHeight);
        base.AddChild(_background);

        // 创建内容容器
        _contentContainer = new Container();
        base.AddChild(_contentContainer);

        // 设置交互
        Interactive = true;
        _background.Interactive = true;

        // 注册事件
        _background.OnMouseOver += OnBackgroundMouseOver;
        _background.OnMouseOut += OnBackgroundMouseOut;
    }

    /// <summary>
    /// 列表项的宽度。
    /// </summary>
    public override float Width
    {
        get => _itemWidth;
        set
        {
            if (_itemWidth != value)
            {
                _itemWidth = value;
                UpdateBackground();
            }
        }
    }

    /// <summary>
    /// 列表项的高度。
    /// </summary>
    public override float Height
    {
        get => _itemHeight;
        set
        {
            if (_itemHeight != value)
            {
                _itemHeight = value;
                UpdateBackground();
            }
        }
    }

    /// <summary>
    /// 普通状态的背景颜色。
    /// </summary>
    public RawColor4 NormalColor
    {
        get => _normalColor;
        set
        {
            _normalColor = value;
            UpdateBackgroundColor();
        }
    }

    /// <summary>
    /// 悬停状态的背景颜色。
    /// </summary>
    public RawColor4 HoverColor
    {
        get => _hoverColor;
        set { _hoverColor = value; }
    }

    /// <summary>
    /// 向列表项添加内容 (会自动添加到内容容器中)。
    /// </summary>
    public void AddContent(DisplayObject content)
    {
        _contentContainer.AddChild(content);
    }

    /// <summary>
    /// 移除列表项的内容。
    /// </summary>
    public void RemoveContent(DisplayObject content)
    {
        _contentContainer.RemoveChild(content);
    }

    /// <summary>
    /// 清除列表项的所有内容。
    /// </summary>
    public void ClearContent()
    {
        _contentContainer.ClearChildren();
    }

    /// <summary>
    /// 更新背景图形。
    /// </summary>
    private void UpdateBackground()
    {
        _background.Clear();
        _background.DrawRectangle(0, 0, _itemWidth, _itemHeight);
        UpdateBackgroundColor();
    }

    /// <summary>
    /// 更新背景颜色 (根据当前状态)。
    /// </summary>
    private void UpdateBackgroundColor()
    {
        if (_isHovered)
        {
            _background.FillColor = _hoverColor;
        }
        else
        {
            _background.FillColor = _normalColor;
        }
    }

    private void OnBackgroundMouseOver(DisplayObjectEvent evt)
    {
        _isHovered = true;
        UpdateBackgroundColor();

    }

    private void OnBackgroundMouseOut(DisplayObjectEvent evt)
    {
        _isHovered = false;
        UpdateBackgroundColor();
    }

    public override void Dispose()
    {
        _background.OnMouseOver -= OnBackgroundMouseOver;
        _background.OnMouseOut -= OnBackgroundMouseOut;
        base.Dispose();
    }
}
