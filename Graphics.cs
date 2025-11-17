using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D;

/// <summary>
/// 用于绘制矢量图形 (矩形、圆形等) 的 DisplayObject。
/// 类似于 PIXI.js 中的 Graphics。
/// </summary>
public class Graphics : DisplayObject
{
    #region 内部形状定义

    private interface IGraphicsShape
    {
        void Render(RenderTarget renderTarget, SolidColorBrush fillBrush, SolidColorBrush strokeBrush, float strokeWidth);
        RectangleF GetBounds(); // 添加获取包围盒的方法
    }
    private class GraphicsRectangle : IGraphicsShape
    {
        public RawRectangleF Rect;
        public void Render(RenderTarget renderTarget, SolidColorBrush fillBrush, SolidColorBrush strokeBrush, float strokeWidth)
        {
            if (fillBrush is not null) renderTarget.FillRectangle(Rect, fillBrush);
            if (strokeBrush is not null && strokeWidth > 0) renderTarget.DrawRectangle(Rect, strokeBrush, strokeWidth);
        }
        public RectangleF GetBounds() => new(Rect.Left,
                                             Rect.Top,
                                             Rect.Right - Rect.Left,
                                             Rect.Bottom - Rect.Top);
    }
    private class GraphicsRoundedRectangle : IGraphicsShape
    {
        public RoundedRectangle RoundedRect;
        public void Render(RenderTarget renderTarget, SolidColorBrush fillBrush, SolidColorBrush strokeBrush, float strokeWidth)
        {
            if (fillBrush is not null) renderTarget.FillRoundedRectangle(RoundedRect, fillBrush);
            if (strokeBrush is not null && strokeWidth > 0) renderTarget.DrawRoundedRectangle(RoundedRect, strokeBrush, strokeWidth);
        }
        public RectangleF GetBounds() => new(RoundedRect.Rect.Left,
                                             RoundedRect.Rect.Top,
                                             RoundedRect.Rect.Right - RoundedRect.Rect.Left,
                                             RoundedRect.Rect.Bottom - RoundedRect.Rect.Top);
    }
    private class GraphicsEllipse : IGraphicsShape
    {
        public Ellipse Ellipse;
        public void Render(RenderTarget renderTarget, SolidColorBrush fillBrush, SolidColorBrush strokeBrush, float strokeWidth)
        {
            if (fillBrush is not null) renderTarget.FillEllipse(Ellipse, fillBrush);
            if (strokeBrush is not null && strokeWidth > 0) renderTarget.DrawEllipse(Ellipse, strokeBrush, strokeWidth);
        }
        public RectangleF GetBounds() => new(Ellipse.Point.X - Ellipse.RadiusX,
                                             Ellipse.Point.Y - Ellipse.RadiusY,
                                             Ellipse.RadiusX * 2,
                                             Ellipse.RadiusY * 2);
    }

    #endregion

    private readonly List<IGraphicsShape> _shapes = [];

    // private RenderTarget? _cachedRenderTarget;

    // 笔刷 (Brushes)
    private SolidColorBrush? _fillBrush;
    private SolidColorBrush? _strokeBrush;

    // 颜色和样式
    private RawColor4 _fillColor = new(0, 0, 0, 0); // 默认透明填充

    private RawColor4 _strokeColor = new(0, 0, 0, 0); // 默认透明边框
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
    /// 获取或设置填充颜色 (包括 Alpha)。
    /// </summary>
    public RawColor4 FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            _isFillDirty = true;
        }
    }

    /// <summary>
    /// 获取或设置边框颜色 (包括 Alpha)。
    /// </summary>
    public RawColor4 StrokeColor
    {
        get => _strokeColor;
        set
        {
            _strokeColor = value;
            _isStrokeDirty = true;
        }
    }

    /// <summary>
    /// 清除所有已绘制的图形。
    /// </summary> 
    public void Clear()
    {
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

        // 注意: 这是一个简化的包围盒检查。
        // 它不检查像素完美的命中，只是检查点是否在包含所有形状的矩形内。
        return _cachedBounds.Contains(localPoint);
    }

    /// <summary>
    /// 更新此 Graphics 对象的包围盒。
    /// </summary>
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

    /// <summary>
    /// 绘制一个矩形。
    /// </summary> 
    public void DrawRectangle(float x, float y, float width, float height)
    {
        _shapes.Add(new GraphicsRectangle
        {
            Rect = new RawRectangleF(x, y, x + width, y + height)
        });
        _boundsDirty = true;
    }

    /// <summary>
    /// 绘制一个圆角矩形。
    /// </summary> 
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

    /// <summary>
    /// 绘制一个椭圆 (或圆形)。
    /// </summary> 
    public void DrawEllipse(float centerX, float centerY, float radiusX, float radiusY)
    {
        _shapes.Add(new GraphicsEllipse
        {
            Ellipse = new Ellipse(new RawVector2(centerX, centerY), radiusX, radiusY)
        });
        _boundsDirty = true;
    }

    /// <summary>
    /// 渲染所有图形。
    /// </summary>
    public override void Render(RenderTarget renderTarget, Matrix3x2 parentTransform)
    {
        if (!Visible || _shapes.Count == 0) return;

        // 1. 计算世界变换 (Matrix3x2)
        Matrix3x2 myLocalTransform = GetLocalTransform();
        Matrix3x2 myWorldTransform = myLocalTransform * parentTransform;

        // 2. 保存并设置变换
        var oldTransform = renderTarget.Transform;
        // 隐式转换为 RawMatrix3x2
        renderTarget.Transform = new RawMatrix3x2
        {
            M11 = myWorldTransform.M11,
            M12 = myWorldTransform.M12,
            M21 = myWorldTransform.M21,
            M22 = myWorldTransform.M22,
            M31 = myWorldTransform.M31,
            M32 = myWorldTransform.M32
        };

        // 3. 管理和更新笔刷 
        //if (!ReferenceEquals(_cachedRenderTarget, renderTarget))
        //{
        //    _fillBrush?.Dispose();
        //    _strokeBrush?.Dispose();
        //    _fillBrush = null;
        //    _strokeBrush = null;
        //    _cachedRenderTarget = renderTarget;
        //    _isFillDirty = true; // 强制重建
        //    _isStrokeDirty = true; // 强制重建
        //}
        if (_isFillDirty)
        {
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
            _isFillDirty = false;
        }
        if (_isStrokeDirty)
        {
            if (_strokeColor.A > 0)
            {
                if (_strokeBrush is null)
                    _strokeBrush = new SolidColorBrush(renderTarget, _strokeColor);
                else
                    _strokeBrush.Color = _strokeColor;
            }
            else
            {
                _strokeBrush?.Dispose();
                _strokeBrush = null;
            }
            _isStrokeDirty = false;
        }

        // 4. 遍历并渲染所有形状
        foreach (var shape in _shapes)
        {
            shape.Render(renderTarget, _fillBrush!, _strokeBrush!, StrokeWidth);
        }

        // 5. 恢复变换
        renderTarget.Transform = oldTransform;
    }

    /// <summary>
    /// 释放笔刷资源。
    /// </summary>
    // ... (Dispose 逻辑不变) ...
    public override void Dispose()
    {
        base.Dispose();
        _fillBrush?.Dispose();
        _strokeBrush?.Dispose();
        _fillBrush = null;
        _strokeBrush = null;
        //_cachedRenderTarget = null;
        Clear();
    }
}