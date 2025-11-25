using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Pixi2D.Core;

/// <summary>
/// 用于绘制矢量图形 (矩形、圆形等) 的 DisplayObject。
/// 类似于 PIXI.js 中的 Graphics。
/// </summary>
public partial class Graphics : DisplayObject
{
    private readonly List<IGraphicsShape> _shapes = [];

    // --- 笔刷 (Brushes) ---
    // 改为基类 Brush 以支持 SolidColorBrush 和 LinearGradientBrush
    private Brush? _activeFillBrush;
    private Brush? _activeStrokeBrush;

    // --- 渐变资源管理 ---
    private GradientStopCollection? _fillGradientStops;
    private GradientStopCollection? _strokeGradientStops;

    private BrushStyle _fillStyle = new(new(0, 0, 0, 0));
    private BrushStyle _strokeStyle = new(new(0, 0, 0, 0));

    private bool _isFillDirty = true;
    private bool _isStrokeDirty = true;

    public float StrokeWidth { get; set; } = 1.0f;

    private RectangleF _cachedBounds = RectangleF.Empty;
    private bool _boundsDirty = true;

    public RectangleF GetBounds(bool forceUpdate = false)
    {
        if (forceUpdate)
            _boundsDirty = true;
        UpdateBounds();
        return _cachedBounds;
    }

    /// <summary>
    /// 获取或设置填充颜色 (纯色)。
    /// 设置此属性会清除任何已设置的渐变填充。
    /// </summary>
    public RawColor4 FillColor
    {
        get => _fillStyle.SolidColor;
        set
        {
            _fillStyle = new(value);
            _isFillDirty = true;
        }
    }

    /// <summary>
    /// 获取或设置边框颜色 (纯色)。
    /// 设置此属性会清除任何已设置的渐变边框。
    /// </summary>
    public RawColor4 StrokeColor
    {
        get => _strokeStyle.SolidColor;
        set
        {
            _strokeStyle = new(value);
            _isStrokeDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the brush style used to fill shapes.
    /// </summary>
    public BrushStyle FillStyle
    {
        get => _fillStyle;
        set
        {
            _fillStyle = value;
            _isFillDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the style used to render the stroke of the shape.
    /// </summary>
    public BrushStyle StrokeStyle
    {
        get => _strokeStyle;
        set
        {
            _strokeStyle = value;
            _isStrokeDirty = true;
        }
    }

    /// <summary>
    /// 清除所有已绘制的图形。
    /// </summary> 
    public void Clear()
    {
        // 清除时必须释放所有形状持有的资源 
        foreach (var shape in _shapes)
        {
            shape.Dispose();
        }
        _shapes.Clear();
        _boundsDirty = true;
    }

    /// <summary>
    /// 检查本地点是否在任何绘制的形状的包围盒内。
    /// </summary>
    public override bool HitTest(PointF localPoint)
    {
        UpdateBounds(); // 确保包围盒是最新的
        if (_cachedBounds.IsEmpty) return false;
        return _cachedBounds.Contains(localPoint);
    }

    private void UpdateBounds()
    {
        if (!_boundsDirty) return;

        if (_shapes.Count == 0)
        {
            _cachedBounds = RectangleF.Empty;
        }
        else
        {
            var firstBounds = _shapes[0].GetBounds();
            float minX = firstBounds.Left, minY = firstBounds.Top;
            float maxX = firstBounds.Right, maxY = firstBounds.Bottom;

            for (int i = 1; i < _shapes.Count; i++)
            {
                var bounds = _shapes[i].GetBounds();
                minX = Math.Min(minX, bounds.Left);
                minY = Math.Min(minY, bounds.Top);
                maxX = Math.Max(maxX, bounds.Right);
                maxY = Math.Max(maxY, bounds.Bottom);
            }
            _cachedBounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }
        _boundsDirty = false;
    }

    // --- 绘图方法 ---

    public void DrawRectangle(float x, float y, float width, float height)
    {
        _shapes.Add(new GraphicsRectangle
        {
            Rect = new RawRectangleF(x, y, x + width, y + height)
        });
        _boundsDirty = true;
    }

    public void DrawRoundedRectangle(float x, float y, float width, float height, float radiusX, float radiusY)
    {
        _shapes.Add(new GraphicsRoundedRectangle
        {
            RoundedRect = new RoundedRectangle
            {
                Rect = new RawRectangleF(x, y, x + width, y + height),
                RadiusX = radiusX,
                RadiusY = radiusY
            }
        });
        _boundsDirty = true;
    }

    public void DrawEllipse(float centerX, float centerY, float radiusX, float radiusY)
    {
        _shapes.Add(new GraphicsEllipse
        {
            Ellipse = new Ellipse(new RawVector2(centerX, centerY), radiusX, radiusY)
        });
        _boundsDirty = true;
    }

    public void DrawCurve(PointF[] points, float tension = 0.0f)
    {
        if (points.Length < 2) return;
        _shapes.Add(new GraphicsCurve
        {
            Points = [.. points],
            Tension = tension
        });
        _boundsDirty = true;
    }

    public void DrawArrowLine(PointF[] points, ArrowType type, float arrowSize, float tension = 0.0f)
    {
        if (points.Length < 2) return;
        _shapes.Add(new GraphicsArrowLine
        {
            Points = [.. points],
            Type = type,
            ArrowSize = arrowSize,
            Tension = tension
        });
        _boundsDirty = true;
    }

    /// <summary>
    /// 渲染所有图形。
    /// </summary>
    public override void Render(RenderTarget renderTarget, ref Matrix3x2 parentTransform)
    {
        if (!Visible || _shapes.Count == 0) return;

        // 1. 变换计算
        uint parentVersion = (Parent != null) ? Parent._worldVersion : 0;
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

        // 2. 设置变换
        var oldTransform = renderTarget.Transform;
        renderTarget.Transform = Unsafe.As<Matrix3x2, RawMatrix3x2>(ref _worldTransform);

        // 2.5 更新包围盒
        UpdateBounds();

        // 3. 更新笔刷资源
        UpdateBrushes(renderTarget);

        // 4. 遍历并渲染所有形状
        // 只有当笔刷非空时才传递，形状内部会判断是否为 null
        foreach (var shape in _shapes)
        {
            shape.Render(renderTarget, _activeFillBrush!, _activeStrokeBrush!, StrokeWidth);
        }

        // 5. 恢复变换
        renderTarget.Transform = oldTransform;
    }

    private void UpdateBrushes(RenderTarget renderTarget)
    {
        // 检查 Fill Brush
        if (_isFillDirty)
        {
            // 清理旧资源
            _activeFillBrush?.Dispose();
            _activeFillBrush = null;
            _fillGradientStops?.Dispose();
            _fillGradientStops = null;

            if (_fillStyle.IsGradient)
            {
                var sX = _fillStyle.IsRelativePosition ? _cachedBounds.Width * _fillStyle.Start.X : _fillStyle.Start.X;
                var sY = _fillStyle.IsRelativePosition ? _cachedBounds.Height * _fillStyle.Start.Y : _fillStyle.Start.Y;
                var eX = _fillStyle.IsRelativePosition ? _cachedBounds.Width * _fillStyle.End.X : _fillStyle.End.X;
                var eY = _fillStyle.IsRelativePosition ? _cachedBounds.Height * _fillStyle.End.Y : _fillStyle.End.Y;

                // 创建线性渐变画笔  
                _fillGradientStops = new GradientStopCollection(renderTarget, _fillStyle.Stops);
                var props = new LinearGradientBrushProperties
                {
                    StartPoint = new RawVector2(sX, sY),
                    EndPoint = new RawVector2(eX, eY)
                };

                _activeFillBrush = new LinearGradientBrush(renderTarget, props, _fillGradientStops);
            }
            else if (_fillStyle.SolidColor.A > 0)
            {
                // 创建纯色画笔
                _activeFillBrush = new SolidColorBrush(renderTarget, _fillStyle.SolidColor);
            }

            _isFillDirty = false;
        }

        // 检查 Stroke Brush
        if (_isStrokeDirty)
        {
            _activeStrokeBrush?.Dispose();
            _activeStrokeBrush = null;
            _strokeGradientStops?.Dispose();
            _strokeGradientStops = null;

            if (_strokeStyle.IsGradient)
            {
                var sX = _strokeStyle.IsRelativePosition ? _cachedBounds.Width * _strokeStyle.Start.X : _strokeStyle.Start.X;
                var sY = _strokeStyle.IsRelativePosition ? _cachedBounds.Height * _strokeStyle.Start.Y : _strokeStyle.Start.Y;
                var eX = _strokeStyle.IsRelativePosition ? _cachedBounds.Width * _strokeStyle.End.X : _strokeStyle.End.X;
                var eY = _strokeStyle.IsRelativePosition ? _cachedBounds.Height * _strokeStyle.End.Y : _strokeStyle.End.Y;

                // 创建线性渐变画笔 
                _strokeGradientStops = new GradientStopCollection(renderTarget, _strokeStyle.Stops);
                var props = new LinearGradientBrushProperties
                {
                    StartPoint = new RawVector2(sX, sY),
                    EndPoint = new RawVector2(eX, eY)
                };

                _activeStrokeBrush = new LinearGradientBrush(renderTarget, props, _strokeGradientStops);
            }
            else if (_strokeStyle.SolidColor.A > 0)
            {
                _activeStrokeBrush = new SolidColorBrush(renderTarget, _strokeStyle.SolidColor);
            }

            _isStrokeDirty = false;
        }
    }

    /// <summary>
    /// 释放笔刷资源。
    /// </summary> 
    public override void Dispose()
    {
        base.Dispose();

        _activeFillBrush?.Dispose();
        _activeStrokeBrush?.Dispose();
        _fillGradientStops?.Dispose();
        _strokeGradientStops?.Dispose();

        _activeFillBrush = null;
        _activeStrokeBrush = null;
        _fillGradientStops = null;
        _strokeGradientStops = null;

        Clear(); // Clear 会负责 dispose 所有的形状
    }
}
