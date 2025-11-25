using Pixi2D.Core;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 面板控件，带有背景和边框的容器。
/// 可用作其他控件的容器或作为可视化边界。
/// </summary>
public class Panel : Container
{
    private readonly Graphics _background;
    private readonly Container _contentContainer;

    private RawColor4 _backgroundColor = new(0.15f, 0.15f, 0.15f, 1.0f);
    private RawColor4 _borderColor = new(0.4f, 0.4f, 0.4f, 1.0f);

    private float _panelWidth = 200f;
    private float _panelHeight = 200f;
    private float _borderWidth = 1f;
    private float _cornerRadius = 0f;

    private float _paddingLeft = 1f;
    private float _paddingTop = 1f;
    private float _paddingRight = 1f;
    private float _paddingBottom = 1f;

    /// <summary>
    /// 创建一个新面板。
    /// </summary>
    public Panel(float width = 200f, float height = 200f)
    {
        _panelWidth = width;
        _panelHeight = height;

        // 创建背景
        _background = new Graphics
        {
            Interactive = false,
            FillColor = _backgroundColor,
            StrokeColor = _borderColor,
            StrokeWidth = _borderWidth
        };
        UpdateBackground();
        AddChild(_background);

        // 创建内容容器
        _contentContainer = new Container
        {
            X = _paddingLeft,
            Y = _paddingTop,
            ClipContent = true  // 默认启用裁剪
        };
        base.AddChild(_contentContainer);
        UpdateClipSize();
    }

    public new bool Interactive
    {
        get => _background.Interactive;
        set => _background.Interactive = value;
    }

    public new bool AcceptFocus
    {
        get => _background.AcceptFocus;
        set => _background.AcceptFocus = value;
    }

    public new DisplayObject? FocusTarget
    {
        get => _background.FocusTarget;
        set => _background.FocusTarget = value;
    }

    public override bool HitTest(PointF localPoint)
    {
        return _background.HitTest(localPoint);
    }

    /// <summary>
    /// 获取或设置是否裁剪超出面板范围的内容。
    /// </summary>
    public new bool ClipContent
    {
        get => _contentContainer.ClipContent;
        set
        {
            _contentContainer.ClipContent = value;
            UpdateClipSize();
        }
    }

    /// <summary>
    /// 更新裁剪区域大小。
    /// </summary>
    private void UpdateClipSize()
    {
        if (_contentContainer.ClipContent)
        {
            _contentContainer.ClipWidth = _panelWidth - _paddingLeft - _paddingRight;
            _contentContainer.ClipHeight = _panelHeight - _paddingTop - _paddingBottom;
        }
    }

    public float MinHeight { get; set; } = 50;
    public float MaxHeight { get; set; } = 0;
    public float MinWidth { get; set; } = 50;
    public float MaxWidth { get; set; } = 0;

    /// <summary>
    /// 面板的宽度。
    /// </summary>
    public override float Width
    {
        get => _panelWidth;
        set
        {
            if (_panelWidth != value)
            {
                _panelWidth = Math.Max(value, MinWidth);
                if (MaxWidth > 0)
                {
                    _panelWidth = Math.Min(_panelWidth, MaxWidth);
                }
                UpdateBackground();
                UpdateClipSize();
                
            }
        }
    }

    /// <summary>
    /// 面板的高度。
    /// </summary>
    public override float Height
    {
        get => _panelHeight;
        set
        {
            if (_panelHeight != value)
            {
                _panelHeight = Math.Max(value, MinHeight);
                if (MaxHeight > 0)
                {
                    _panelHeight = Math.Min(_panelHeight, MaxHeight);
                }
                UpdateBackground();
                UpdateClipSize();
            }
        }
    }

    /// <summary>
    /// 背景颜色。
    /// </summary>
    public RawColor4 BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            _background.FillColor = value;
        }
    }

    /// <summary>
    /// 边框颜色。
    /// </summary>
    public RawColor4 BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            _background.StrokeColor = value;
        }
    }

    /// <summary>
    /// 边框宽度。
    /// </summary>
    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = value;
            _background.StrokeWidth = value;
        }
    }

    /// <summary>
    /// 圆角半径。
    /// </summary>
    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = value;
            UpdateBackground();
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
            _paddingLeft = value;
            _contentContainer.X = value;
            UpdateClipSize();
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
            _paddingTop = value;
            _contentContainer.Y = value;
            UpdateClipSize();
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
            _paddingRight = value;
            UpdateClipSize();
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
            _paddingBottom = value;
            UpdateClipSize();
        }
    }

    /// <summary>
    /// 设置所有内边距为相同值。
    /// </summary>
    public void SetPadding(float padding)
    {
        _paddingLeft = _paddingTop = _paddingRight = _paddingBottom = padding;
        _contentContainer.X = padding;
        _contentContainer.Y = padding;
        UpdateClipSize();
    }

    /// <summary>
    /// 设置内边距 (上下相同，左右相同)。
    /// </summary>
    public void SetPadding(float vertical, float horizontal)
    {
        _paddingTop = _paddingBottom = vertical;
        _paddingLeft = _paddingRight = horizontal;
        _contentContainer.X = horizontal;
        _contentContainer.Y = vertical;
        UpdateClipSize();
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
        _contentContainer.X = left;
        _contentContainer.Y = top;
        UpdateClipSize();
    }

    /// <summary>
    /// 向面板添加内容 (会自动添加到内容容器中)。
    /// </summary>
    public void AddContent(DisplayObject content)
    {
        _contentContainer.AddChild(content);
    }

    /// <summary>
    /// 移除面板的内容。
    /// </summary>
    public void RemoveContent(DisplayObject content)
    {
        _contentContainer.RemoveChild(content);
    }

    /// <summary>
    /// 清除面板的所有内容。
    /// </summary>
    public void ClearContent()
    {
        _contentContainer.ClearChildren();
    }

    /// <summary>
    /// 获取内容容器 (可用于高级布局)。
    /// </summary>
    public Container ContentContainer => _contentContainer;

    /// <summary>
    /// 更新背景图形。
    /// </summary>
    private void UpdateBackground()
    {
        _background.Clear();
        _background.FillColor = _backgroundColor;
        _background.StrokeColor = _borderColor;
        _background.StrokeWidth = _borderWidth;

        if (_cornerRadius > 0)
        {
            _background.DrawRoundedRectangle(0, 0, _panelWidth, _panelHeight, _cornerRadius, _cornerRadius);
        }
        else
        {
            _background.DrawRectangle(0, 0, _panelWidth, _panelHeight);
        }
    }
}
