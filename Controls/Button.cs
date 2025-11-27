using Pixi2D.Core;
using Pixi2D.Events;
using SharpDX.Mathematics.Interop;

namespace Pixi2D.Controls;

/// <summary>
/// 可点击的按钮控件,包含背景和文本。
/// 支持悬停效果和点击事件。
/// </summary>
public class Button : Container
{
    private readonly Graphics _background;
    private readonly Text _label;

    private BrushStyle _normalStyle = new(new RawColor4(0.3f, 0.3f, 0.3f, 1.0f));
    private BrushStyle _hoverStyle = new(new RawColor4(0.4f, 0.4f, 0.4f, 1.0f));
    private BrushStyle _pressedStyle = new(new RawColor4(0.2f, 0.2f, 0.2f, 1.0f));
    private BrushStyle _borderStyle = new(new RawColor4(0.5f, 0.5f, 0.5f, 1.0f));
    private BrushStyle _hoverBorderStyle = new(new RawColor4(0.8f, 0.8f, 0.8f, 1.0f));
    private BrushStyle _focusedBorderStyle = new(new RawColor4(0.0f, 0.6f, 1.0f, 1.0f));

    private bool _isHovered = false;
    private bool _isPressed = false;

    private float _buttonWidth = 100f;
    private float _buttonHeight = 30f;
    private float _borderRadius = 5f;

    // Border properties
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
            Interactive = true,
            FillStyle = _normalStyle,
        };
        _background.DrawRoundedRectangle(0, 0, _buttonWidth, _buttonHeight, _borderRadius, _borderRadius);
        base.AddChild(_background);

        // 创建文本标签
        _label = text;

        _label.MaxWidth = _buttonWidth - 10; // 留出边距
        _label.X = 5;
        _label.Y = (_buttonHeight - 14f) / 2; // 垂直居中 (大约)
        base.AddChild(_label);

        // 设置交互
        Interactive = true;
        AcceptFocus = true;

        // 注册事件
        _background.OnMouseOver += OnBackgroundMouseOver;
        _background.OnMouseOut += OnBackgroundMouseOut;
        _background.OnMouseDown += OnBackgroundMouseDown;
        _background.OnMouseUp += OnBackgroundMouseUp;
        _background.OnClick += OnBackgroundClick;

        this.OnKeyDown += HandleKeyEvent;
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
                _bgDirty = true;
                _textPositionDirty = true;
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
                _bgDirty = true;
                _textPositionDirty = true;
            }
        }
    }

    /// <summary>
    /// 按钮文本。
    /// </summary>
    public string Text
    {
        get => _label.Content;
        set
        {
            _label.Content = value;
            _textPositionDirty = true;
        }
    }

    /// <summary>
    /// 按钮圆角半径。
    /// </summary>
    public float BorderRadius
    {
        get => _borderRadius;
        set
        {
            _borderRadius = value;
            _bgDirty = true;
        }
    }

    /// <summary>
    /// 边框颜色。
    /// </summary>
    public BrushStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            _bgDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the border style applied when the control is hovered over by the pointer.
    /// </summary>
    public BrushStyle HoverBorderStyle
    {
        get => _hoverBorderStyle;
        set => _hoverBorderStyle = value;
    }

    /// <summary>
    /// Gets or sets the border style applied when the control is focused.
    /// </summary>
    public BrushStyle FocusedBorderStyle
    {
        get => _focusedBorderStyle;
        set => _focusedBorderStyle = value;
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
            _bgDirty = true;
        }
    }

    /// <summary>
    /// 悬停状态的背景颜色。
    /// </summary>
    public BrushStyle HoverStyle
    {
        get => _hoverStyle;
        set => _hoverStyle = value;
    }

    public BrushStyle NormalStyle
    {
        get => _normalStyle;
        set
        {
            _normalStyle = value;
            _bgDirty = true;
        }
    }

    /// <summary>
    /// 按下状态的背景颜色。
    /// </summary>
    public BrushStyle PressedStyle
    {
        get => _pressedStyle;
        set { _pressedStyle = value; }
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
        _bgDirty = false;
        _background.Clear();
        _background.DrawRoundedRectangle(0, 0, _buttonWidth, _buttonHeight, _borderRadius, _borderRadius);

        // Set border properties
        if (_hasFocus)
        {
            _background.StrokeStyle = _focusedBorderStyle;
        }
        else if (_isHovered)
        {
            _background.StrokeStyle = _hoverBorderStyle;
        }
        else
        {
            _background.StrokeStyle = _borderStyle;
        }


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
            _background.FillStyle = _pressedStyle;
        }
        else if (_isHovered)
        {
            _background.FillStyle = _hoverStyle;
            _background.StrokeStyle = _hoverBorderStyle;
        }
        else
        {
            _background.FillStyle = _normalStyle;
            _background.StrokeStyle = _hasFocus ? _focusedBorderStyle : _borderStyle;
        }
    }

    /// <summary>
    /// 更新标签位置 (根据对齐方式)。
    /// </summary>
    private void UpdateLabelPosition()
    {
        var rect = _label.GetTextRect();
        if (rect.Width == 0 && rect.Height == 0)
        {
            // 文本为空时不更新位置
            return;
        }

        var x = (_buttonWidth - rect.Width) / 2;
        var y = (_buttonHeight - rect.Height) / 2;

        _label.SetPosition(x, y);
        _textPositionDirty = false;
    }

    private void HandleKeyEvent(DisplayObjectEvent @event)
    {
        if (@event.Data is null) return;
        if (@event.Data.KeyCode == 13 || @event.Data.KeyCode == 32) // Enter or Space
        {
            OnButtonClick?.Invoke(this);
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

    private bool _bgDirty = true;
    private bool _hasFocus = false;
    private bool _textPositionDirty = true;
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);
        var hasFocus = IsFocused();
        if (hasFocus != _hasFocus || _bgDirty)
        {
            _hasFocus = hasFocus;
            UpdateBackground();
        }
        if (_textPositionDirty)
        {
            UpdateLabelPosition();
        }
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
