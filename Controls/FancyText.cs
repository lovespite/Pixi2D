using Pixi2D.Core;
using Pixi2D.Extensions;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Pixi2D.Controls;

/// <summary>
/// 一个增强的文本控件，支持渐变色填充（文本、背景、边框）、内边距 (Padding) 以及自动调整背景大小。
/// </summary>
public class FancyText : DisplayObject
{
    // --- 核心资源 ---
    private readonly SharpDX.DirectWrite.Factory _dwFactory;
    private TextFormat? _textFormat;
    private TextLayout? _textLayout;
    private RenderTarget? _cachedRenderTarget;

    // --- 文本属性 ---
    private string _text;
    private string _fontFamily;
    private float _fontSize;
    private FontStyle _fontStyle;
    private FontWeight _fontWeight;
    private bool _wordWrap = false;
    private float _maxWidth = float.MaxValue;
    private bool _isLayoutDirty = true;

    // --- 样式属性 ---
    private float _paddingLeft = 0f;
    private float _paddingTop = 0f;
    private float _paddingRight = 0f;
    private float _paddingBottom = 0f;
    private float _cornerRadius = 0f;
    private float _borderWidth = 0f;

    // --- 尺寸缓存 ---
    private float _calculatedWidth;
    private float _calculatedHeight;

    // --- 渐变/颜色定义 ---
    private BrushStyle _textStyle = new(new RawColor4(1, 1, 1, 1)); // 默认白色文字
    private BrushStyle _backgroundStyle = new(new RawColor4(0, 0, 0, 0)); // 默认透明背景
    private BrushStyle _borderStyle = new(new RawColor4(0, 0, 0, 0)); // 默认透明边框

    // --- 运行时画笔缓存 ---
    private Brush? _textBrush;
    private Brush? _backgroundBrush;
    private Brush? _borderBrush;

    // 保存 GradientStopCollection 以便释放
    private GradientStopCollection? _textGradientStops;
    private GradientStopCollection? _bgGradientStops;
    private GradientStopCollection? _borderGradientStops;

    private bool _isBrushDirty = true;

    /// <summary>
    /// 使用指定的工厂创建 FancyText。
    /// </summary>
    public FancyText(Factory factory) : this(factory.DwfInstance)
    {
        // 从工厂复制默认样式
        _fontFamily = factory.FontFamily;
        _fontSize = factory.FontSize;
        _fontStyle = factory.FontStyle;
        _fontWeight = factory.FontWeight;
        TextColor = factory.FillColor.ToRawColor4();
    }

    public FancyText(SharpDX.DirectWrite.Factory dwFactory, string text = "", string fontFamily = "Arial", float fontSize = 16f, FontStyle fontStyle = FontStyle.Normal, FontWeight fontWeight = FontWeight.Regular)
    {
        _dwFactory = dwFactory;
        _text = text;
        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _fontStyle = fontStyle;
        _fontWeight = fontWeight;
    }

    #region Public Properties (Text & Font)

    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; InvalidateLayout(); } }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { if (_fontFamily != value) { _fontFamily = value; InvalidateLayout(); } }
    }

    public float FontSize
    {
        get => _fontSize;
        set { if (_fontSize != value) { _fontSize = value; InvalidateLayout(); } }
    }

    public FontStyle FontStyle
    {
        get => _fontStyle;
        set { if (_fontStyle != value) { _fontStyle = value; InvalidateLayout(); } }
    }

    public FontWeight FontWeight
    {
        get => _fontWeight;
        set { if (_fontWeight != value) { _fontWeight = value; InvalidateLayout(); } }
    }

    public bool WordWrap
    {
        get => _wordWrap;
        set { if (_wordWrap != value) { _wordWrap = value; InvalidateLayout(); } }
    }

    /// <summary>
    /// 文本的最大宽度（用于换行）。注意：这不包括 Padding。
    /// </summary>
    public float MaxTextWidth
    {
        get => _maxWidth;
        set { if (_maxWidth != value) { _maxWidth = value; InvalidateLayout(); } }
    }

    #endregion

    #region Public Properties (Padding & Sizing)

    public float Padding
    {
        set => SetPadding(value);
    }

    public float PaddingLeft
    {
        get => _paddingLeft;
        set { if (_paddingLeft != value) { _paddingLeft = value; InvalidateLayout(); } }
    }
    public float PaddingTop
    {
        get => _paddingTop;
        set { if (_paddingTop != value) { _paddingTop = value; InvalidateLayout(); } }
    }
    public float PaddingRight
    {
        get => _paddingRight;
        set { if (_paddingRight != value) { _paddingRight = value; InvalidateLayout(); } }
    }
    public float PaddingBottom
    {
        get => _paddingBottom;
        set { if (_paddingBottom != value) { _paddingBottom = value; InvalidateLayout(); } }
    }

    public void SetPadding(float padding)
    {
        _paddingLeft = _paddingTop = _paddingRight = _paddingBottom = padding;
        InvalidateLayout();
    }

    public void SetPadding(float horizontal, float vertical)
    {
        _paddingLeft = _paddingRight = horizontal;
        _paddingTop = _paddingBottom = vertical;
        InvalidateLayout();
    }

    public void SetPadding(float left, float top, float right, float bottom)
    {
        _paddingLeft = left;
        _paddingTop = top;
        _paddingRight = right;
        _paddingBottom = bottom;
        InvalidateLayout();
    }

    /// <summary>
    /// 控件的总宽度（文本宽度 + Padding）。
    /// </summary>
    public override float Width
    {
        get
        {
            UpdateLayoutIfNeeded();
            return _calculatedWidth;
        }
        set { /* 只读，由内容决定 */ }
    }

    /// <summary>
    /// 控件的总高度（文本高度 + Padding）。
    /// </summary>
    public override float Height
    {
        get
        {
            UpdateLayoutIfNeeded();
            return _calculatedHeight;
        }
        set { /* 只读，由内容决定 */ }
    }

    #endregion

    #region Public Properties (Appearance)

    public float CornerRadius
    {
        get => _cornerRadius;
        set { if (_cornerRadius != value) { _cornerRadius = value; Invalidate(); } }
    }

    public float BorderWidth
    {
        get => _borderWidth;
        set { if (_borderWidth != value) { _borderWidth = value; Invalidate(); } }
    }

    // --- 快捷颜色设置器 (纯色) ---

    /// <summary>
    /// Sets the color used to render text.
    /// </summary>
    /// <remarks>Use this property to change the text color to a solid color value. Setting this property will
    /// update the rendering style for subsequent text output.</remarks>
    public RawColor4 TextColor
    {
        set { _textStyle = new BrushStyle(value); _isBrushDirty = true; }
    }

    /// <summary>
    /// Sets the background color for the control.
    /// </summary>
    /// <remarks>Setting this property updates the control's background brush. The change will take effect the
    /// next time the control is rendered.</remarks>
    public RawColor4 BackgroundColor
    {
        set { _backgroundStyle = new BrushStyle(value); _isBrushDirty = true; }
    }

    /// <summary>
    /// Sets the color used for the border of the brush.
    /// </summary>
    public RawColor4 BorderColor
    {
        set { _borderStyle = new BrushStyle(value); _isBrushDirty = true; }
    }

    // --- 完整 BrushStyle 设置器 ---

    /// <summary>
    /// Gets or sets the brush style used to render the text.
    /// </summary>
    public BrushStyle TextBrush
    {
        get => _textStyle;
        set { _textStyle = value; _isBrushDirty = true; }
    }

    /// <summary>
    /// Gets or sets the brush style used to render the background.
    /// </summary>
    public BrushStyle BackgroundStyle
    {
        get => _backgroundStyle;
        set { _backgroundStyle = value; _isBrushDirty = true; }
    }

    /// <summary>
    /// Gets or sets the brush style used to render the border.
    /// </summary>
    public BrushStyle BorderStyle
    {
        get => _borderStyle;
        set { _borderStyle = value; _isBrushDirty = true; }
    }

    #endregion

    #region Layout & Rendering

    private void InvalidateLayout()
    {
        _isLayoutDirty = true;
        Invalidate(); // 标记 DisplayObject 为 dirty
    }

    private void UpdateLayoutIfNeeded()
    {
        if (!_isLayoutDirty && _textLayout != null) return;

        // 1. 释放旧资源
        _textFormat?.Dispose();
        _textLayout?.Dispose();

        // 2. 创建 TextFormat
        _textFormat = new TextFormat(_dwFactory, _fontFamily, _fontWeight, _fontStyle, _fontSize)
        {
            WordWrapping = _wordWrap ? WordWrapping.Wrap : WordWrapping.NoWrap
        };

        // 3. 创建 TextLayout
        _textLayout = new TextLayout(_dwFactory, _text ?? "", _textFormat, _maxWidth, float.MaxValue);

        // 4. 计算尺寸
        var metrics = _textLayout.Metrics;
        // 如果文本为空，metrics 可能是 0，给个最小高度
        float textW = metrics.Width;
        float textH = metrics.Height;

        // 修正: 即使空文本也应保留高度
        if (string.IsNullOrEmpty(_text))
        {
            textH = _fontSize;
        }

        _calculatedWidth = textW + _paddingLeft + _paddingRight;
        _calculatedHeight = textH + _paddingTop + _paddingBottom;

        _isLayoutDirty = false;
    }

    public override bool HitTest(PointF localPoint)
    {
        UpdateLayoutIfNeeded();
        // 简单的矩形碰撞检测
        return localPoint.X >= 0 && localPoint.X <= _calculatedWidth &&
               localPoint.Y >= 0 && localPoint.Y <= _calculatedHeight;
    }

    public override void Render(RenderTarget renderTarget, ref Matrix3x2 parentTransform)
    {
        if (!Visible) return;

        // 1. 标准变换计算 (复制自 DisplayObject)
        uint parentVersion = (Parent != null) ? Parent._worldVersion : 0;
        if (_localDirty || parentVersion != _parentVersion)
        {
            if (_localDirty) { _localTransform = CalculateLocalTransform(); _localDirty = false; }
            _worldTransform = _localTransform * parentTransform;
            _parentVersion = parentVersion;
            _worldVersion++;
            _worldDirty = false;
        }
        else if (_worldDirty)
        {
            _worldTransform = _localTransform * parentTransform;
            _worldDirty = false;
        }

        // 2. 布局更新
        UpdateLayoutIfNeeded();

        // 3. 资源更新 (画笔)
        UpdateBrushes(renderTarget);

        if (_textLayout == null) return;

        // 4. 设置变换
        var oldTransform = renderTarget.Transform;
        renderTarget.Transform = Unsafe.As<Matrix3x2, RawMatrix3x2>(ref _worldTransform);

        // 5. 绘制
        var bgRect = new RawRectangleF(0, 0, _calculatedWidth, _calculatedHeight);

        // --- 绘制背景 ---
        if (_backgroundBrush != null)
        {
            if (_cornerRadius > 0)
            {
                renderTarget.FillRoundedRectangle(
                    new RoundedRectangle { Rect = bgRect, RadiusX = _cornerRadius, RadiusY = _cornerRadius },
                    _backgroundBrush
                );
            }
            else
            {
                renderTarget.FillRectangle(bgRect, _backgroundBrush);
            }
        }

        // --- 绘制边框 ---
        if (_borderBrush != null && _borderWidth > 0)
        {
            if (_cornerRadius > 0)
            {
                renderTarget.DrawRoundedRectangle(
                    new RoundedRectangle { Rect = bgRect, RadiusX = _cornerRadius, RadiusY = _cornerRadius },
                    _borderBrush,
                    _borderWidth
                );
            }
            else
            {
                renderTarget.DrawRectangle(bgRect, _borderBrush, _borderWidth);
            }
        }

        // --- 绘制文本 ---
        if (_textBrush != null)
        {
            // 文本需要根据 Padding 偏移
            renderTarget.DrawTextLayout(
                new RawVector2(_paddingLeft, _paddingTop),
                _textLayout,
                _textBrush,
                DrawTextOptions.None
            );
        }

        // 6. 恢复变换
        renderTarget.Transform = oldTransform;
    }

    private void UpdateBrushes(RenderTarget renderTarget)
    {
        // 如果 RenderTarget 变了，必须重建资源
        if (_cachedRenderTarget != renderTarget)
        {
            _cachedRenderTarget = renderTarget;
            _isBrushDirty = true;
            // 清理旧的 Device 依赖资源
            DisposeBrushes();
        }

        if (!_isBrushDirty) return;

        // 重建画笔辅助方法
        Brush CreateBrush(BrushStyle style, ref GradientStopCollection? stopsCache)
        {
            stopsCache?.Dispose();
            stopsCache = null;

            if (style.IsGradient)
            {
                var sX = style.IsRelativePosition ? style.Start.X * Width : style.Start.X;
                var sY = style.IsRelativePosition ? style.Start.Y * Height : style.Start.Y;
                var eX = style.IsRelativePosition ? style.End.X * Width : style.End.X;
                var eY = style.IsRelativePosition ? style.End.Y * Height : style.End.Y;

                stopsCache = new GradientStopCollection(renderTarget, style.Stops);
                var props = new LinearGradientBrushProperties
                {
                    StartPoint = new RawVector2(sX, sY),
                    EndPoint = new RawVector2(eX, eY)
                };
                return new LinearGradientBrush(renderTarget, props, stopsCache) { Opacity = Alpha };
            }
            else
            {
                return new SolidColorBrush(renderTarget, style.SolidColor) { Opacity = Alpha };
            }
        }

        // 1. Text Brush
        _textBrush?.Dispose();
        _textBrush = CreateBrush(_textStyle, ref _textGradientStops);

        // 2. Background Brush
        _backgroundBrush?.Dispose();
        // 只有当颜色不完全透明，或者定义了渐变时才创建背景画笔
        if (_backgroundStyle.IsGradient || _backgroundStyle.SolidColor.A > 0)
        {
            _backgroundBrush = CreateBrush(_backgroundStyle, ref _bgGradientStops);
        }
        else
        {
            _backgroundBrush = null;
        }

        // 3. Border Brush
        _borderBrush?.Dispose();
        if (_borderStyle.IsGradient || _borderStyle.SolidColor.A > 0)
        {
            _borderBrush = CreateBrush(_borderStyle, ref _borderGradientStops);
        }
        else
        {
            _borderBrush = null;
        }

        _isBrushDirty = false;
    }

    private void DisposeBrushes()
    {
        _textBrush?.Dispose();
        _backgroundBrush?.Dispose();
        _borderBrush?.Dispose();
        _textGradientStops?.Dispose();
        _bgGradientStops?.Dispose();
        _borderGradientStops?.Dispose();

        _textBrush = null;
        _backgroundBrush = null;
        _borderBrush = null;
        _textGradientStops = null;
        _bgGradientStops = null;
        _borderGradientStops = null;
    }

    public override void Dispose()
    {
        base.Dispose();
        _textFormat?.Dispose();
        _textLayout?.Dispose();
        DisposeBrushes();
        _cachedRenderTarget = null;
    }

    #endregion 

    #region Factory

    public class Factory(SharpDX.DirectWrite.Factory dwFactory)
    {
        public SharpDX.DirectWrite.Factory DwfInstance { get; } = dwFactory;
        public string FontFamily { get; set; } = "Arial";
        public float FontSize { get; set; } = 16f;
        public FontStyle FontStyle { get; set; } = FontStyle.Normal;
        public FontWeight FontWeight { get; set; } = FontWeight.Regular;
        private float _paddingLeft = 1f;
        private float _paddingRight = 1f;
        private float _paddingTop = 1f;
        private float _paddingBottom = 1f;
        public float Padding { set => _paddingLeft = _paddingRight = _paddingTop = _paddingBottom = value; }
        public float PaddingHorizontal { set => _paddingLeft = _paddingRight = value; }
        public float PaddingVertical { set => _paddingTop = _paddingBottom = value; }
        public float PaddingLeft { get => _paddingLeft; set => _paddingLeft = value; }
        public float PaddingRight { get => _paddingRight; set => _paddingRight = value; }
        public float PaddingTop { get => _paddingTop; set => _paddingTop = value; }
        public float PaddingBottom { get => _paddingBottom; set => _paddingBottom = value; }
        public Color FillColor
        {
            get => _fillColor;
            set
            {
                _fillColor = value;
                _textStyle = new BrushStyle(value.ToRawColor4());
            }
        }

        public static Factory From(Text.Factory textFactory)
        {
            return new Factory(textFactory.DwfInstance)
            {
                FontFamily = textFactory.FontFamily,
                FontSize = textFactory.FontSize,
                FontStyle = textFactory.FontStyle,
                FontWeight = textFactory.FontWeight,
                FillColor = textFactory.FillColor,
            };
        }

        private BrushStyle _textStyle = new(new RawColor4(1, 1, 1, 1)); // 默认白色文字
        private BrushStyle _backgroundStyle = new(new RawColor4(0, 0, 0, 0)); // 默认透明背景
        private BrushStyle _borderStyle = new(new RawColor4(0, 0, 0, 0)); // 默认透明边框
        private Color _fillColor = Color.White;

        public FancyText Create(string text = "")
        {
            var fancyText = new FancyText(DwfInstance, text, fontFamily: FontFamily, fontSize: FontSize, fontStyle: FontStyle, fontWeight: FontWeight)
            {
                _textStyle = _textStyle,
                _backgroundStyle = _backgroundStyle,
                _borderStyle = _borderStyle,
                _isBrushDirty = true,
                _paddingLeft = _paddingLeft,
                _paddingRight = _paddingRight,
                _paddingBottom = _paddingBottom,
                _paddingTop = _paddingTop,
                _isLayoutDirty = true,
            };
            return fancyText;
        }

        public FancyText Create(string text, Color fillColor)
        {
            var fancyText = new FancyText(DwfInstance, text, fontFamily: FontFamily, fontSize: FontSize, fontStyle: FontStyle, fontWeight: FontWeight)
            {
                _textStyle = new BrushStyle(fillColor.ToRawColor4()),
                _backgroundStyle = _backgroundStyle,
                _borderStyle = _borderStyle,
                _isBrushDirty = true,
                _paddingLeft = _paddingLeft,
                _paddingRight = _paddingRight,
                _paddingBottom = _paddingBottom,
                _paddingTop = _paddingTop,
                _isLayoutDirty = true,
            };
            return fancyText;
        }

        public FancyText Create(string text, float fontSize)
        {
            var fancyText = new FancyText(DwfInstance, text, fontFamily: FontFamily, fontSize: fontSize, fontStyle: FontStyle, fontWeight: FontWeight)
            {
                _textStyle = _textStyle,
                _backgroundStyle = _backgroundStyle,
                _borderStyle = _borderStyle,
                _isBrushDirty = true,
                _paddingLeft = _paddingLeft,
                _paddingRight = _paddingRight,
                _paddingBottom = _paddingBottom,
                _paddingTop = _paddingTop,
                _isLayoutDirty = true,
            };
            return fancyText;
        }

        public FancyText Create(string text, float fontSize, Color fillColor)
        {
            var fancyText = new FancyText(DwfInstance, text, fontFamily: FontFamily, fontSize: fontSize, fontStyle: FontStyle, fontWeight: FontWeight)
            {
                _textStyle = new BrushStyle(fillColor.ToRawColor4()),
                _backgroundStyle = _backgroundStyle,
                _borderStyle = _borderStyle,
                _isBrushDirty = true,
                _paddingLeft = _paddingLeft,
                _paddingRight = _paddingRight,
                _paddingBottom = _paddingBottom,
                _paddingTop = _paddingTop,
                _isLayoutDirty = true,
            };
            return fancyText;
        }

        /// <summary>
        /// 设置文本的线性渐变。坐标是相对于文本本身的（0,0 是文本左上角）。
        /// </summary>
        public void SetTextBrushStyle(BrushStyle style)
        {
            _textStyle = style;
        }

        /// <summary>
        /// 设置背景的线性渐变。坐标是相对于控件整体的（0,0 是控件左上角）。
        /// </summary>
        public void SetBackgroundBrushStyle(BrushStyle style)
        {
            _backgroundStyle = style;
        }

        /// <summary>
        /// 设置边框的线性渐变。坐标是相对于控件整体的。
        /// </summary>
        public void SetBorderBrushStyle(BrushStyle style)
        {
            _borderStyle = style;
        }

        public Factory Clone()
        {
            return new Factory(DwfInstance)
            {
                FontFamily = FontFamily,
                FontSize = FontSize,
                FontStyle = FontStyle,
                FontWeight = FontWeight,
                FillColor = FillColor,
                _textStyle = _textStyle,
                _backgroundStyle = _backgroundStyle,
                _borderStyle = _borderStyle
            };
        }
    }

    #endregion
}