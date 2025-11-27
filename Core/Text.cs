using Pixi2D.Extensions;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Pixi2D.Core;

/// <summary>
/// 用于显示文本的 DisplayObject。<br />
/// 基于 DirectWrite。
/// </summary>
public class Text : DisplayObject
{
    private RenderTarget? _cachedRenderTarget;

    // Brushes
    private SolidColorBrush? _fillBrush;
    private SolidColorBrush? _backgroundBrush;
    private SolidColorBrush? _borderBrush;

    private TextFormat? _textFormat;
    private TextLayout? _textLayout;

    private bool _isDirty = true; // 标记是否需要重建 Format 和 Layout
    private bool _isBrushDirty = true; // 标记是否需要更新笔刷颜色

    private string _text;
    private string _fontFamily;
    private float _fontSize;
    private FontStyle _fontStyle;
    private FontWeight _fontWeight;

    // Colors
    private RawColor4 _fillColor;
    private RawColor4 _backgroundColor = new(0, 0, 0, 0); // 默认透明
    private RawColor4 _borderColor = new(0, 0, 0, 0); // 默认透明

    // Layout & Style
    private float _maxWidth = float.MaxValue;
    private float _maxHeight = float.MaxValue;
    private WordWrapping _wordWrapping = WordWrapping.Wrap;
    private float _borderWidth = 0f;
    private float _borderRadius = 0f;

    // --- 公共属性 --- 
    // (注意: Text 自己的属性 setter 也会调用 Invalidate(),
    //  这会正确地使 DisplayObject 的 _localDirty 标记生效)

    public string Content
    {
        get => _text;
        set { if (_text != value) { _text = value; _isDirty = true; Invalidate(); } } // Invalidate
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { if (_fontFamily != value) { _fontFamily = value; _isDirty = true; Invalidate(); } } // Invalidate
    }

    public float FontSize
    {
        get => _fontSize;
        set { if (_fontSize != value) { _fontSize = value; _isDirty = true; Invalidate(); } } // Invalidate
    }

    public FontStyle FontStyle
    {
        get => _fontStyle;
        set { if (_fontStyle != value) { _fontStyle = value; _isDirty = true; Invalidate(); } } // Invalidate
    }

    public FontWeight FontWeight
    {
        get => _fontWeight;
        set { if (_fontWeight != value) { _fontWeight = value; _isDirty = true; Invalidate(); } } // Invalidate
    }

    public RawColor4 FillColor
    {
        get => _fillColor;
        set { _fillColor = value; _isBrushDirty = true; } // 颜色不影响变换, 无需 Invalidate()
    }

    public RawColor4 BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; _isBrushDirty = true; }
    }

    public RawColor4 BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; _isBrushDirty = true; }
    }

    public float BorderWidth
    {
        get => _borderWidth;
        set { _borderWidth = value; Invalidate(); } // 边框宽度可能影响视觉边界(虽然不影响TextLayout本身)
    }

    public float BorderRadius
    {
        get => _borderRadius;
        set { _borderRadius = value; Invalidate(); }
    }

    public float MaxWidth
    {
        get => _maxWidth;
        set { if (_maxWidth != value) { _maxWidth = value; _isDirty = true; Invalidate(); } } // Invalidate
    }

    public float MaxHeight
    {
        get => _maxHeight;
        set { if (_maxHeight != value) { _maxHeight = value; _isDirty = true; Invalidate(); } } // Invalidate
    }

    /// <summary>
    /// 获取或设置文本是否自动换行。
    /// true = 自动换行 (Wrap), false = 不换行 (NoWrap).
    /// </summary>
    public bool WordWrap
    {
        get => _wordWrapping == WordWrapping.Wrap;
        set
        {
            var newMode = value ? WordWrapping.Wrap : WordWrapping.NoWrap;
            if (_wordWrapping != newMode)
            {
                _wordWrapping = newMode;
                _isDirty = true; // 强制重建 format 和 layout
                Invalidate(); // Invalidate
            }
        }
    }

    private readonly SharpDX.DirectWrite.Factory _dwFactory;

    /// <summary>
    /// 获取当前的 TextFormat。如果需要，会重新创建。
    /// </summary>
    public TextFormat GetTextFormat()
    {
        if (_textFormat is null || _isDirty)
        {
            _textFormat?.Dispose();
            _textFormat =
                new TextFormat(
                                factory: _dwFactory,
                                fontFamilyName: _fontFamily,
                                fontSize: _fontSize,
                                fontWeight: _fontWeight,
                                fontStyle: _fontStyle)
                {
                    WordWrapping = WordWrapping.Wrap
                };
            _isDirty = true; // 标记布局仍需更新
        }

        return _textFormat;
    }

    /// <summary>
    /// (新增) 获取当前或更新后的 TextLayout。
    /// </summary>
    public TextLayout? GetTextLayout(RenderTarget? renderTarget = null)
    {
        var rt = renderTarget ?? _cachedRenderTarget ?? GetStage()?.GetCachedRenderTarget();
        if ((_textLayout is null || _isDirty) && rt is not null)
        {
            UpdateResources(rt);
        }
        return _textLayout;
    }

    private TextLayout GetTextLayoutInternal()
    {
        if (_textLayout is null || _isDirty)
        {
            _textLayout?.Dispose();
            _textLayout = new TextLayout(_dwFactory, _text ?? string.Empty, GetTextFormat(), _maxWidth, _maxHeight);
            _isDirty = false; // 布局已更新
        }

        return _textLayout;
    }

    /// <summary>
    /// 创建一个新的 Text 对象。
    /// </summary>
    /// <param name="text">要显示的文本。</param>
    /// <param name="fontFamily">字体家族 (例如 "Arial")。</param>
    /// <param name="fontSize">字体大小。</param>
    /// <param name="color">文本颜色。</param>
    public Text(SharpDX.DirectWrite.Factory dwFactory, string text, string fontFamily, float fontSize, FontStyle style, FontWeight weight, RawColor4 color)
    {
        _text = text;
        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _fillColor = color;
        _fontStyle = style;
        _fontWeight = weight;
        _dwFactory = dwFactory;
    }

    /// <summary>
    /// 确保所有 DWrite 资源都已创建并为最新。
    /// </summary>
    private void UpdateResources(RenderTarget renderTarget)
    {
        // 检查 RenderTarget 是否已更改 (例如设备丢失)
        if (_cachedRenderTarget != renderTarget)
        {
            _fillBrush?.Dispose();
            _backgroundBrush?.Dispose();
            _borderBrush?.Dispose();

            _fillBrush = null;
            _backgroundBrush = null;
            _borderBrush = null;

            _cachedRenderTarget = renderTarget;
            _isBrushDirty = true; // 强制重建笔刷
        }

        // 检查文本样式/内容是否已更改
        if (_isDirty)
        {
            GetTextLayoutInternal(); // 使用现有方法重建布局 
        }

        // 检查笔刷颜色是否已更改
        if (_isBrushDirty)
        {
            // 1. Fill Brush
            if (_fillColor.A > 0)
            {
                if (_fillBrush is null)
                    _fillBrush = new SolidColorBrush(renderTarget, _fillColor);
                else
                    _fillBrush.Color = _fillColor;
            }
            else
            {
                _fillBrush?.Dispose();
                _fillBrush = null;
            }

            // 2. Background Brush
            if (_backgroundColor.A > 0)
            {
                if (_backgroundBrush is null)
                    _backgroundBrush = new SolidColorBrush(renderTarget, _backgroundColor);
                else
                    _backgroundBrush.Color = _backgroundColor;
            }
            else
            {
                _backgroundBrush?.Dispose();
                _backgroundBrush = null;
            }

            // 3. Border Brush
            if (_borderColor.A > 0)
            {
                if (_borderBrush is null)
                    _borderBrush = new SolidColorBrush(renderTarget, _borderColor);
                else
                    _borderBrush.Color = _borderColor;
            }
            else
            {
                _borderBrush?.Dispose();
                _borderBrush = null;
            }

            _isBrushDirty = false;
        }
    }

    public TextMetrics GetTextRect(bool forceUpdate = false, RenderTarget? renderTarget = null)
    {
        if (forceUpdate)
        {
            var rt = renderTarget ?? _cachedRenderTarget ?? GetStage()?.GetCachedRenderTarget();

            // 确保 TextLayout 存在，即使尚未渲染
            // 注意: 第一次调用可能需要一个有效的 RenderTarget
            if (_textLayout is null && rt is not null)
            {
                UpdateResources(rt);
            }
        }

        if (_textLayout is null) return default;

        return _textLayout.Metrics;
    }

    /// <summary>
    /// 检查本地点是否在文本布局的边界内。
    /// </summary>
    public override bool HitTest(PointF localPoint)
    {
        // 确保 TextLayout 存在，即使尚未渲染
        // 注意: 第一次 HitTest 可能需要一个有效的 RenderTarget
        if (_textLayout is null && _cachedRenderTarget is not null)
        {
            UpdateResources(_cachedRenderTarget);
        }

        if (_textLayout is null) return false;

        // 使用布局的指标进行简单的 AABB 检查
        var metrics = _textLayout.Metrics;
        // 如果有边框宽度，命中测试也应该考虑进去吗？通常文本点击是基于内容的。
        // 这里我们使用 Metrics 的宽高，它包含了文本的实际占用范围。
        return localPoint.X >= metrics.Left && localPoint.X < metrics.Left + metrics.Width &&
               localPoint.Y >= metrics.Top && localPoint.Y < metrics.Top + metrics.Height;
    }

    /// <summary>
    /// (已优化) 渲染文本。
    /// </summary>
    public override void Render(RenderTarget renderTarget, ref Matrix3x2 parentTransform)
    {
        if (!Visible) return;

        // 1. (优化) 计算或获取缓存的变换
        uint parentVersion = (Parent is not null) ? Parent._worldVersion : 0;
        bool parentDirty = (parentVersion != _parentVersion);

        if (_localDirty || parentDirty)
        {
            if (_localDirty)
            {
                _localTransform = CalculateLocalTransform();
                _localDirty = false;
            }
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
        // ... 否则, _worldTransform 已经是最新的。


        // 2. 确保所有 DWrite 资源 (Format, Layout, Brush) 都是最新的
        UpdateResources(renderTarget);

        if (_textLayout is null) return;

        // 3. 保存并设置变换
        var oldTransform = renderTarget.Transform;
        // (优化) 使用缓存的 _worldTransform 
        renderTarget.Transform = Unsafe.As<Matrix3x2, RawMatrix3x2>(ref _worldTransform);

        // 获取文本尺寸
        var metrics = _textLayout.Metrics;
        float width = metrics.Width;
        float height = metrics.Height;

        // --- 绘制背景 ---
        if (_backgroundBrush is not null)
        {
            _backgroundBrush.Opacity = Alpha;
            if (_borderRadius > 0)
            {
                renderTarget.FillRoundedRectangle(
                    new RoundedRectangle { Rect = new RawRectangleF(0, 0, width, height), RadiusX = _borderRadius, RadiusY = _borderRadius },
                    _backgroundBrush
                );
            }
            else
            {
                renderTarget.FillRectangle(new RawRectangleF(0, 0, width, height), _backgroundBrush);
            }
        }

        // --- 绘制文本 ---
        if (_fillBrush is not null)
        {
            _fillBrush.Opacity = Alpha;
            renderTarget.DrawTextLayout(
                new RawVector2(0, 0),
                _textLayout,
                _fillBrush,
                DrawTextOptions.None // 或 DrawTextOptions.Clip (如果需要)
            );
        }

        // --- 绘制边框 ---
        if (_borderBrush is not null && _borderWidth > 0)
        {
            _borderBrush.Opacity = Alpha;
            if (_borderRadius > 0)
            {
                renderTarget.DrawRoundedRectangle(
                    new RoundedRectangle { Rect = new RawRectangleF(0, 0, width, height), RadiusX = _borderRadius, RadiusY = _borderRadius },
                    _borderBrush,
                    _borderWidth
                );
            }
            else
            {
                renderTarget.DrawRectangle(new RawRectangleF(0, 0, width, height), _borderBrush, _borderWidth);
            }
        }

        // 5. 恢复变换
        renderTarget.Transform = oldTransform;
    }

    /// <summary>
    /// 释放 DWrite 和 D2D 资源。
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        _textFormat?.Dispose();
        _textLayout?.Dispose();
        _fillBrush?.Dispose();
        _backgroundBrush?.Dispose();
        _borderBrush?.Dispose();

        _textFormat = null;
        _textLayout = null;
        _fillBrush = null;
        _backgroundBrush = null;
        _borderBrush = null;
        _cachedRenderTarget = null;
    }

    public class Factory(SharpDX.DirectWrite.Factory factory)
    {    // 静态 DirectWrite 工厂 (为简单起见，所有 Text 实例共享)
        private static readonly SharpDX.DirectWrite.Factory s_dwFactory = new();

        internal static SharpDX.DirectWrite.Factory Shared => s_dwFactory;

        private readonly SharpDX.DirectWrite.Factory m_dwFactory = factory;

        internal SharpDX.DirectWrite.Factory DwfInstance => m_dwFactory;

        public Factory() : this(Shared)
        {
        }

        public string FontFamily { get; set; } = "Arial";
        public FontWeight FontWeight { get; set; } = FontWeight.Regular;
        public FontStyle FontStyle { get; set; } = FontStyle.Normal;
        public float FontSize { get; set; } = 16f;
        public Color FillColor { get; set; } = Color.White;

        public Text Create() => Create(string.Empty);

        public Text Create(string content)
        {
            return new Text(
                            m_dwFactory,
                            text: content,
                            fontFamily: FontFamily,
                            fontSize: FontSize,
                            weight: FontWeight,
                            style: FontStyle,
                            color: FillColor.ToRawColor4());
        }

        public Text Create(string text, float fontSize)
        {
            return new Text(
                            m_dwFactory,
                            text: text,
                            fontFamily: FontFamily,
                            fontSize: fontSize,
                            weight: FontWeight,
                            style: FontStyle,
                            color: FillColor.ToRawColor4());
        }

        public Text Create(string text, float fontSize, Color fillColor)
        {
            return new Text(
                            m_dwFactory,
                            text: text,
                            fontFamily: FontFamily,
                            fontSize: fontSize,
                            weight: FontWeight,
                            style: FontStyle,
                            color: fillColor.ToRawColor4());
        }

        public Text Create(string text, FontStyle fontStyle)
        {
            return new Text(
                            m_dwFactory,
                            text: text,
                            fontFamily: FontFamily,
                            fontSize: FontSize,
                            weight: FontWeight,
                            style: fontStyle,
                            color: FillColor.ToRawColor4());
        }

        public Factory Clone()
        {
            return new Factory(m_dwFactory)
            {
                FontFamily = this.FontFamily,
                FontSize = this.FontSize,
                FontStyle = this.FontStyle,
                FontWeight = this.FontWeight,
                FillColor = this.FillColor
            };
        }
    }
}