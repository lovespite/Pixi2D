using Pixi2D.Events;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 可点击的按钮控件,包含背景和文本。
/// 支持悬停效果和点击事件。
/// </summary>
public class Button : Container
{
    private readonly Graphics _background;
    private readonly Text _label;

    private RawColor4 _normalColor = new(0.3f, 0.3f, 0.3f, 1.0f);
    private RawColor4 _hoverColor = new(0.4f, 0.4f, 0.4f, 1.0f);
    private RawColor4 _pressedColor = new(0.2f, 0.2f, 0.2f, 1.0f);

    private bool _isHovered = false;
    private bool _isPressed = false;

    private float _buttonWidth = 100f;
    private float _buttonHeight = 30f;
    private float _cornerRadius = 5f;

    // Border properties
    private RawColor4 _borderColor = new(0.5f, 0.5f, 0.5f, 1.0f);
    private float _borderWidth = 0f;

    /// <summary>
    /// 当按钮被点击时触发。
    /// </summary>
    public event Action<Button>? OnButtonClick;

    /// <summary>
    /// 创建一个新按钮。
    /// </summary>
    public Button(Text text, float width = 100f, float height = 30f)
    {
        _buttonWidth = width;
        _buttonHeight = height;

        // 创建背景
        _background = new Graphics
        {
            Interactive = true
        };
        _background.FillColor = _normalColor;
        _background.DrawRoundedRectangle(0, 0, _buttonWidth, _buttonHeight, _cornerRadius, _cornerRadius);
        base.AddChild(_background);

        // 创建文本标签
        _label = text;

        _label.MaxWidth = _buttonWidth - 10; // 留出边距
        _label.X = 5;
        _label.Y = (_buttonHeight - 14f) / 2; // 垂直居中 (大约)
        base.AddChild(_label);

        // 设置交互
        Interactive = true;
        _background.Interactive = true;

        // 注册事件
        _background.OnMouseOver += OnBackgroundMouseOver;
        _background.OnMouseOut += OnBackgroundMouseOut;
        _background.OnMouseDown += OnBackgroundMouseDown;
        _background.OnMouseUp += OnBackgroundMouseUp;
        _background.OnClick += OnBackgroundClick;
    }

    /// <summary>
    /// 按钮的宽度。
    /// </summary>
    public override float Width
    {
        get => _buttonWidth;
        set
        {
            if (_buttonWidth != value)
            {
                _buttonWidth = value;
                UpdateBackground();
                UpdateLabelPosition();
            }
        }
    }

    /// <summary>
    /// 按钮的高度。
    /// </summary>
    public override float Height
    {
        get => _buttonHeight;
        set
        {
            if (_buttonHeight != value)
            {
                _buttonHeight = value;
                UpdateBackground();
                UpdateLabelPosition();
            }
        }
    }

    /// <summary>
    /// 按钮文本。
    /// </summary>
    public string Text
    {
        get => _label.Content;
        set => _label.Content = value;
    }

    /// <summary>
    /// 按钮圆角半径。
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
    /// 边框圆角半径 (与 CornerRadius 相同)。
    /// </summary>
    public float BorderRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = value;
            UpdateBackground();
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
            UpdateBackground();
        }
    }

    /// <summary>
    /// 边框宽度。设置为 0 表示无边框。
    /// </summary>
    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = value;
            UpdateBackground();
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
    /// 按下状态的背景颜色。
    /// </summary>
    public RawColor4 PressedColor
    {
        get => _pressedColor;
        set { _pressedColor = value; }
    }

    /// <summary>
    /// 文本颜色。
    /// </summary>
    public RawColor4 TextColor
    {
        get => _label.FillColor;
        set => _label.FillColor = value;
    }

    /// <summary>
    /// 更新背景图形。
    /// </summary>
    private void UpdateBackground()
    {
        _background.Clear();
        _background.DrawRoundedRectangle(0, 0, _buttonWidth, _buttonHeight, _cornerRadius, _cornerRadius);

        // Set border properties
        _background.StrokeColor = _borderColor;
        _background.StrokeWidth = _borderWidth;

        UpdateBackgroundColor();
    }

    /// <summary>
    /// 更新背景颜色 (根据当前状态)。
    /// </summary>
    private void UpdateBackgroundColor()
    {
        if (_isPressed)
        {
            _background.FillColor = _pressedColor;
        }
        else if (_isHovered)
        {
            _background.FillColor = _hoverColor;
        }
        else
        {
            _background.FillColor = _normalColor;
        }
    }

    /// <summary>
    /// 更新标签位置 (根据对齐方式)。
    /// </summary>
    private void UpdateLabelPosition()
    {
        const float horizontalPadding = 5f;
        _label.MaxWidth = _buttonWidth - (horizontalPadding * 2);
        // Calculate horizontal position based on alignment 
        _label.X = horizontalPadding;
        // Vertical centering
        _label.Y = (_buttonHeight - _label.FontSize) / 2;
    }

    private void OnBackgroundMouseOver(DisplayObjectEvent evt)
    {
        _isHovered = true;
        UpdateBackgroundColor();
    }

    private void OnBackgroundMouseOut(DisplayObjectEvent evt)
    {
        _isHovered = false;
        _isPressed = false;
        UpdateBackgroundColor();
    }

    private void OnBackgroundMouseDown(DisplayObjectEvent evt)
    {
        _isPressed = true;
        UpdateBackgroundColor();
    }

    private void OnBackgroundMouseUp(DisplayObjectEvent evt)
    {
        _isPressed = false;
        UpdateBackgroundColor();
    }

    private void OnBackgroundClick(DisplayObjectEvent evt)
    {
        OnButtonClick?.Invoke(this);
    }

    public override void Dispose()
    {
        _background.OnMouseOver -= OnBackgroundMouseOver;
        _background.OnMouseOut -= OnBackgroundMouseOut;
        _background.OnMouseDown -= OnBackgroundMouseDown;
        _background.OnMouseUp -= OnBackgroundMouseUp;
        _background.OnClick -= OnBackgroundClick;
        base.Dispose();
    }
}
