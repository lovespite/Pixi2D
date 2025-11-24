using Pixi2D.Core;
using Pixi2D.Events;
using Pixi2D.Extensions;
using SharpDX.Direct2D1;
using System.Drawing;

namespace Pixi2D.Controls;

using D2DBitmap = SharpDX.Direct2D1.Bitmap1;

/// <summary>
/// 基于图像的可点击按钮控件。
/// 支持普通、悬停和按下状态的不同图像，以及点击事件。
/// </summary>
public class FancyButton : Container
{
    private readonly Graphics _bg;
    private readonly Sprite _sprite;
    public Sprite Sprite => _sprite;

    private D2DBitmap? _normalTexture;
    private D2DBitmap? _hoverTexture;
    private D2DBitmap? _pressedTexture;

    private bool _isHovered = false;
    private bool _isPressed = false;

    private bool _bgDirty = true;
    private float _padding = 0f;
    private float _borderRadius = 0f;
    private float _buttonWidth;
    private float _buttonHeight;
    private Shape _buttonShape = Shape.Round;

    /// <summary>
    /// 当按钮被点击时触发。
    /// </summary>
    public event Action<FancyButton>? OnButtonClick;

    public enum Shape
    {
        Round,
        Rectangle,
    }

    /// <summary>
    /// 创建一个新的图像按钮。
    /// </summary>
    /// <param name="normalTexture">普通状态的纹理。</param>
    /// <param name="hoverTexture">悬停状态的纹理 (可选，如果为 null 则使用 normalTexture)。</param>
    /// <param name="pressedTexture">按下状态的纹理 (可选，如果为 null 则使用 hoverTexture 或 normalTexture)。</param>
    public FancyButton(D2DBitmap normalTexture, D2DBitmap? hoverTexture = null, D2DBitmap? pressedTexture = null)
    {
        _bg = new Graphics()
        {
            Interactive = false,
        };
        _normalTexture = normalTexture ?? throw new ArgumentNullException(nameof(normalTexture));
        _hoverTexture = hoverTexture;
        _pressedTexture = pressedTexture;

        // 使用普通纹理的尺寸作为按钮尺寸
        _buttonWidth = normalTexture.Size.Width + _padding * 2;
        _buttonHeight = normalTexture.Size.Height + _padding * 2;

        // 创建 Sprite 显示纹理
        _sprite = new Sprite(normalTexture, disposeBitmapWithSprite: false)
        {
            Interactive = true,
            X = _padding,
            Y = _padding,
        };
        AddChildren(_bg, _sprite);

        // 设置交互
        Interactive = true;
        _sprite.Interactive = true;

        // 注册事件
        _sprite.OnMouseOver += OnSpriteMouseOver;
        _sprite.OnMouseOut += OnSpriteMouseOut;
        _sprite.OnMouseDown += OnSpriteMouseDown;
        _sprite.OnMouseUp += OnSpriteMouseUp;
        _sprite.OnClick += OnSpriteClick;
    }

    public Graphics Background => _bg;

    public float Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            _sprite.X = _padding;
            _sprite.Y = _padding;
            _buttonWidth = (_normalTexture?.Size.Width ?? 0) + _padding * 2;
            _buttonHeight = (_normalTexture?.Size.Height ?? 0) + _padding * 2;
            _bgDirty = true;
        }
    }

    public float BorderWitdh
    {
        get => _bg.StrokeWidth; 
        set => _bg.StrokeWidth = value;
    }

    public float BorderRadius
    {
        get => _borderRadius;
        set
        {
            _borderRadius = value;
            _bgDirty = true;
        }
    }

    public Color BorderColor
    {
        get => _bg.StrokeColor.ToColor(); 
        set => _bg.StrokeColor = value.ToRawColor4();
    }

    public Color BackgroundColor
    {
        get => _bg.FillColor.ToColor(); 
        set => _bg.FillColor = value.ToRawColor4();
    }

    public Shape ButtonShape
    {
        get => _buttonShape;
        set
        {
            _buttonShape = value;
            _bgDirty = true;
        }
    }

    public void UpdateBackground()
    {
        _bg.Clear();
        _bgDirty = false;

        switch (_buttonShape)
        {
            case Shape.Round:
                _bg.DrawEllipse(_buttonWidth / 2, _buttonHeight / 2, _buttonWidth / 2, _buttonHeight / 2);
                break;
            case Shape.Rectangle:
            default:
                _bg.DrawRoundedRectangle(0, 0, _buttonWidth, _buttonHeight, _borderRadius, _borderRadius);
                break;
        }
    }

    /// <summary>
    /// 按钮的宽度 (基于纹理尺寸)。
    /// </summary>
    public override float Width
    {
        get => _buttonWidth;
        set
        {
            // 图像按钮的尺寸由纹理决定，不支持直接修改
            // 如果需要缩放，应该使用 ScaleX/ScaleY 属性
        }
    }

    /// <summary>
    /// 按钮的高度 (基于纹理尺寸)。
    /// </summary>
    public override float Height
    {
        get => _buttonHeight;
        set
        {
            // 图像按钮的尺寸由纹理决定，不支持直接修改
            // 如果需要缩放，应该使用 ScaleX/ScaleY 属性
        }
    }

    /// <summary>
    /// 普通状态的纹理。
    /// </summary>
    public D2DBitmap? NormalTexture
    {
        get => _normalTexture;
        set
        {
            _normalTexture = value;
            UpdateTexture();
        }
    }

    /// <summary>
    /// 悬停状态的纹理。
    /// </summary>
    public D2DBitmap? HoverTexture
    {
        get => _hoverTexture;
        set => _hoverTexture = value;
    }

    /// <summary>
    /// 按下状态的纹理。
    /// </summary>
    public D2DBitmap? PressedTexture
    {
        get => _pressedTexture;
        set => _pressedTexture = value;
    }

    /// <summary>
    /// 更新 Sprite 显示的纹理 (根据当前状态)。
    /// </summary>
    private void UpdateTexture()
    {
        if (_isPressed && _pressedTexture is not null)
        {
            _sprite.Bitmap = _pressedTexture;
        }
        else if (_isHovered && _hoverTexture is not null)
        {
            _sprite.Bitmap = _hoverTexture;
        }
        else if (_normalTexture is not null)
        {
            _sprite.Bitmap = _normalTexture;
        }
    }

    private void OnSpriteMouseOver(DisplayObjectEvent evt)
    {
        _isHovered = true;
        OnMouseOver?.Invoke(evt);
        UpdateTexture();
    }

    private void OnSpriteMouseOut(DisplayObjectEvent evt)
    {
        _isHovered = false;
        _isPressed = false;
        OnMouseOut?.Invoke(evt);
        UpdateTexture();
    }

    private void OnSpriteMouseDown(DisplayObjectEvent evt)
    {
        _isPressed = true;
        OnMouseDown?.Invoke(evt);
        UpdateTexture();
    }

    private void OnSpriteMouseUp(DisplayObjectEvent evt)
    {
        _isPressed = false;
        OnMouseUp?.Invoke(evt);
        UpdateTexture();
    }

    private void OnSpriteClick(DisplayObjectEvent evt)
    {
        OnButtonClick?.Invoke(this);
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (!_bgDirty) return;
        UpdateBackground();
    }

    public override void Dispose()
    {
        _sprite.OnMouseOver -= OnSpriteMouseOver;
        _sprite.OnMouseOut -= OnSpriteMouseOut;
        _sprite.OnMouseDown -= OnSpriteMouseDown;
        _sprite.OnMouseUp -= OnSpriteMouseUp;
        _sprite.OnClick -= OnSpriteClick;
        base.Dispose();
    }
}
