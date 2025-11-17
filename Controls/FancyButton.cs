using Pixi2D.Events;
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
    private readonly Sprite _sprite;

    private D2DBitmap? _normalTexture;
    private D2DBitmap? _hoverTexture;
    private D2DBitmap? _pressedTexture;

    private bool _isHovered = false;
    private bool _isPressed = false;

    private float _buttonWidth;
    private float _buttonHeight;

    /// <summary>
    /// 当按钮被点击时触发。
    /// </summary>
    public event Action<FancyButton>? OnButtonClick;

    /// <summary>
    /// 创建一个新的图像按钮。
    /// </summary>
    /// <param name="normalTexture">普通状态的纹理。</param>
    /// <param name="hoverTexture">悬停状态的纹理 (可选，如果为 null 则使用 normalTexture)。</param>
    /// <param name="pressedTexture">按下状态的纹理 (可选，如果为 null 则使用 hoverTexture 或 normalTexture)。</param>
    public FancyButton(D2DBitmap normalTexture, D2DBitmap? hoverTexture = null, D2DBitmap? pressedTexture = null)
    {
        _normalTexture = normalTexture ?? throw new ArgumentNullException(nameof(normalTexture));
        _hoverTexture = hoverTexture;
        _pressedTexture = pressedTexture;

        // 使用普通纹理的尺寸作为按钮尺寸
        _buttonWidth = normalTexture.Size.Width;
        _buttonHeight = normalTexture.Size.Height;

        // 创建 Sprite 显示纹理
        _sprite = new Sprite(normalTexture, disposeBitmapWithSprite: false)
        {
            Interactive = true
        };
        base.AddChild(_sprite);

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
        UpdateTexture();
    }

    private void OnSpriteMouseOut(DisplayObjectEvent evt)
    {
        _isHovered = false;
        _isPressed = false;
        UpdateTexture();
    }

    private void OnSpriteMouseDown(DisplayObjectEvent evt)
    {
        _isPressed = true;
        UpdateTexture();
    }

    private void OnSpriteMouseUp(DisplayObjectEvent evt)
    {
        _isPressed = false;
        UpdateTexture();
    }

    private void OnSpriteClick(DisplayObjectEvent evt)
    {
        OnButtonClick?.Invoke(this);
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
