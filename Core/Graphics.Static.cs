using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D.Core;
// Graphics 内部形状定义

partial class Graphics
{
    #region 内部形状定义
    /// <summary>
    /// 箭头的类型。
    /// </summary>
    public enum ArrowType
    {
        None,
        /// <summary>
        /// 单向箭头 (在终点)。
        /// </summary>
        Single,
        /// <summary>
        /// 双向箭头 (起点和终点)。
        /// </summary>
        Dual
    }

    // 修改接口继承 IDisposable，以便清理缓存的非托管资源 (Geometry)
    private interface IGraphicsShape : IDisposable
    {
        void Render(RenderTarget renderTarget, Brush fillBrush, Brush strokeBrush, float strokeWidth);
        RectangleF GetBounds(); // 添加获取包围盒的方法
    }

    private class GraphicsRectangle : IGraphicsShape
    {
        public RawRectangleF Rect;
        public void Render(RenderTarget renderTarget, Brush fillBrush, Brush strokeBrush, float strokeWidth)
        {
            if (fillBrush is not null) renderTarget.FillRectangle(Rect, fillBrush);
            if (strokeBrush is not null && strokeWidth > 0) renderTarget.DrawRectangle(Rect, strokeBrush, strokeWidth);
        }
        public RectangleF GetBounds() => new(Rect.Left,
                                             Rect.Top,
                                             Rect.Right - Rect.Left,
                                             Rect.Bottom - Rect.Top);
        public void Dispose() { } // 简单形状无资源需释放
    }

    private class GraphicsRoundedRectangle : IGraphicsShape
    {
        public RoundedRectangle RoundedRect;
        public void Render(RenderTarget renderTarget, Brush fillBrush, Brush strokeBrush, float strokeWidth)
        {
            if (fillBrush is not null) renderTarget.FillRoundedRectangle(RoundedRect, fillBrush);
            if (strokeBrush is not null && strokeWidth > 0) renderTarget.DrawRoundedRectangle(RoundedRect, strokeBrush, strokeWidth);
        }
        public RectangleF GetBounds() => new(RoundedRect.Rect.Left,
                                             RoundedRect.Rect.Top,
                                             RoundedRect.Rect.Right - RoundedRect.Rect.Left,
                                             RoundedRect.Rect.Bottom - RoundedRect.Rect.Top);
        public void Dispose() { }
    }

    private class GraphicsEllipse : IGraphicsShape
    {
        public Ellipse Ellipse;
        public void Render(RenderTarget renderTarget, Brush fillBrush, Brush strokeBrush, float strokeWidth)
        {
            if (fillBrush is not null) renderTarget.FillEllipse(Ellipse, fillBrush);
            if (strokeBrush is not null && strokeWidth > 0) renderTarget.DrawEllipse(Ellipse, strokeBrush, strokeWidth);
        }
        public RectangleF GetBounds() => new(Ellipse.Point.X - Ellipse.RadiusX,
                                             Ellipse.Point.Y - Ellipse.RadiusY,
                                             Ellipse.RadiusX * 2,
                                             Ellipse.RadiusY * 2);
        public void Dispose() { }
    }

    private class GraphicsCurve : IGraphicsShape
    {
        public PointF[] Points = [];
        public float Tension;

        // 缓存 PathGeometry 
        private PathGeometry? _cachedGeometry;

        public void Render(RenderTarget renderTarget, Brush fillBrush, Brush strokeBrush, float strokeWidth)
        {
            if (Points.Length < 2 || (strokeBrush is null && fillBrush is null)) return;

            // 检查缓存是否有效。如果 Factory 改变了（极少见，但可能发生在设备丢失重建时），也需要重建。 
            if (_cachedGeometry is null || _cachedGeometry.Factory.NativePointer != renderTarget.Factory.NativePointer)
            {
                // 释放旧资源
                Dispose();
                // 重建几何体
                _cachedGeometry = BuildGeometry(renderTarget.Factory);
            }

            // 绘制路径 
            if (strokeBrush is not null && strokeWidth > 0)
            {
                renderTarget.DrawGeometry(_cachedGeometry, strokeBrush, strokeWidth);
            }
        }

        private PathGeometry BuildGeometry(Factory factory)
        {
            var geometry = new PathGeometry(factory);
            using var sink = geometry.Open();

            sink.BeginFigure(new RawVector2(Points[0].X, Points[0].Y), FigureBegin.Hollow);

            // 计算 Cardinal Spline (基数样条) 的张力系数
            float scale = (1.0f - Tension) / 2.0f;

            for (int i = 0; i < Points.Length - 1; i++)
            {
                PointF p0 = (i == 0) ? Points[0] : Points[i - 1];
                PointF p1 = Points[i];
                PointF p2 = Points[i + 1];
                PointF p3 = (i == Points.Length - 2) ? Points[i + 1] : Points[i + 2];

                float t1x = (p2.X - p0.X) * scale;
                float t1y = (p2.Y - p0.Y) * scale;
                float t2x = (p3.X - p1.X) * scale;
                float t2y = (p3.Y - p1.Y) * scale;

                float c1x = p1.X + t1x / 3.0f;
                float c1y = p1.Y + t1y / 3.0f;
                float c2x = p2.X - t2x / 3.0f;
                float c2y = p2.Y - t2y / 3.0f;

                sink.AddBezier(new BezierSegment()
                {
                    Point1 = new RawVector2(c1x, c1y),
                    Point2 = new RawVector2(c2x, c2y),
                    Point3 = new RawVector2(p2.X, p2.Y)
                });
            }

            sink.EndFigure(FigureEnd.Open);
            sink.Close();

            return geometry;
        }

        public RectangleF GetBounds()
        {
            if (Points.Length == 0) return RectangleF.Empty;
            float minX = Points[0].X, minY = Points[0].Y, maxX = Points[0].X, maxY = Points[0].Y;
            foreach (var p in Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public void Dispose()
        {
            _cachedGeometry?.Dispose();
            _cachedGeometry = null;
        }
    }

    private class GraphicsArrowLine : IGraphicsShape
    {
        public PointF[] Points = [];
        public float Tension;
        public ArrowType Type;
        public float ArrowSize;

        private PathGeometry? _cachedLineGeometry;
        private PathGeometry? _cachedArrowGeometry;

        public void Render(RenderTarget renderTarget, Brush fillBrush, Brush strokeBrush, float strokeWidth)
        {
            if (Points.Length < 2) return;

            // 重建几何体
            if (_cachedLineGeometry is null || _cachedLineGeometry.Factory.NativePointer != renderTarget.Factory.NativePointer)
            {
                Dispose();
                BuildGeometries(renderTarget.Factory);
            }

            // 1. 绘制线条 (Stroke)
            if (strokeBrush is not null && strokeWidth > 0 && _cachedLineGeometry is not null)
            {
                renderTarget.DrawGeometry(_cachedLineGeometry, strokeBrush, strokeWidth);
            }

            // 2. 绘制箭头 (Fill) - 使用 StrokeColor 填充箭头
            if (strokeBrush is not null && _cachedArrowGeometry is not null)
            {
                renderTarget.FillGeometry(_cachedArrowGeometry, strokeBrush);
            }
        }

        private void BuildGeometries(Factory factory)
        {
            _cachedLineGeometry = new PathGeometry(factory);

            // --- 1. 构建线条路径 ---
            {
                using var sink = _cachedLineGeometry.Open();

                // 计算箭头高度
                float arrowHeight = ArrowSize * 0.866f; // sqrt(3)/2

                // 根据箭头类型调整起点
                PointF startPoint = Points[0];
                if (Type == ArrowType.Dual && Points.Length >= 2)
                {
                    Vector2 startDir = Vector2.Normalize(new Vector2(Points[1].X - Points[0].X, Points[1].Y - Points[0].Y));
                    startPoint = new PointF(Points[0].X + startDir.X * arrowHeight, Points[0].Y + startDir.Y * arrowHeight);
                }

                // 根据箭头类型调整终点
                PointF endPoint = Points[^1];
                if ((Type == ArrowType.Single || Type == ArrowType.Dual) && Points.Length >= 2)
                {
                    int last = Points.Length - 1;
                    Vector2 endDir = Vector2.Normalize(new Vector2(Points[last].X - Points[last - 1].X, Points[last].Y - Points[last - 1].Y));
                    endPoint = new PointF(Points[last].X - endDir.X * arrowHeight, Points[last].Y - endDir.Y * arrowHeight);
                }

                sink.BeginFigure(new RawVector2(startPoint.X, startPoint.Y), FigureBegin.Hollow);

                float scale = (1.0f - Tension) / 2.0f;
                for (int i = 0; i < Points.Length - 1; i++)
                {
                    PointF p0 = (i == 0) ? Points[0] : Points[i - 1];
                    PointF p1 = Points[i];
                    PointF p2 = Points[i + 1];
                    PointF p3 = (i == Points.Length - 2) ? Points[i + 1] : Points[i + 2];

                    float t1x = (p2.X - p0.X) * scale;
                    float t1y = (p2.Y - p0.Y) * scale;
                    float t2x = (p3.X - p1.X) * scale;
                    float t2y = (p3.Y - p1.Y) * scale;

                    float c1x = p1.X + t1x / 3.0f;
                    float c1y = p1.Y + t1y / 3.0f;
                    float c2x = p2.X - t2x / 3.0f;
                    float c2y = p2.Y - t2y / 3.0f;

                    // 如果是最后一段且有终点箭头，调整终点
                    PointF finalPoint = (i == Points.Length - 2 && (Type == ArrowType.Single || Type == ArrowType.Dual))
                        ? endPoint
                        : p2;

                    sink.AddBezier(new BezierSegment
                    {
                        Point1 = new RawVector2(c1x, c1y),
                        Point2 = new RawVector2(c2x, c2y),
                        Point3 = new RawVector2(finalPoint.X, finalPoint.Y)
                    });
                }
                sink.EndFigure(FigureEnd.Open);
                sink.Close();
            }

            // --- 2. 构建箭头路径 ---
            {
                if (Type != ArrowType.None)
                {
                    _cachedArrowGeometry = new PathGeometry(factory);
                    using var sink = _cachedArrowGeometry.Open();
                    // 计算起点方向 (P0 -> P1 的切线)
                    if (Type == ArrowType.Dual)
                    {
                        Vector2 dir = Vector2.Normalize(new Vector2(Points[0].X - Points[1].X, Points[0].Y - Points[1].Y));
                        DrawTriangle(sink, Points[0], dir, ArrowSize);
                    }

                    // 计算终点方向 (P(n-1) -> P(n) 的切线)
                    if (Type == ArrowType.Single || Type == ArrowType.Dual)
                    {
                        int last = Points.Length - 1;
                        Vector2 dir = Vector2.Normalize(new Vector2(Points[last].X - Points[last - 1].X, Points[last].Y - Points[last - 1].Y));
                        DrawTriangle(sink, Points[last], dir, ArrowSize);
                    }
                    sink.Close();
                }
            }
        }

        private static void DrawTriangle(GeometrySink sink, PointF tip, Vector2 direction, float size)
        {
            // 箭头是一个实心等边三角形
            // tip: 顶点
            // direction: 指向顶点的方向

            // 计算底边中心
            float height = size * 0.866f; // sqrt(3)/2
            float halfWidth = size * 0.5f;

            Vector2 tipV = new(tip.X, tip.Y);
            Vector2 baseCenter = tipV - direction * height;

            // 计算垂直向量
            Vector2 perp = new(-direction.Y, direction.X);

            Vector2 c1 = baseCenter + perp * halfWidth;
            Vector2 c2 = baseCenter - perp * halfWidth;

            sink.BeginFigure(new RawVector2(tip.X, tip.Y), FigureBegin.Filled);
            sink.AddLine(new RawVector2(c1.X, c1.Y));
            sink.AddLine(new RawVector2(c2.X, c2.Y));
            sink.EndFigure(FigureEnd.Closed);
        }

        public RectangleF GetBounds()
        {
            if (Points.Length == 0) return RectangleF.Empty;

            // 简单计算包含点的包围盒，加上箭头大小的余量
            float minX = Points[0].X, minY = Points[0].Y, maxX = Points[0].X, maxY = Points[0].Y;
            foreach (var p in Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            float padding = ArrowSize;
            return new RectangleF(minX - padding, minY - padding, (maxX - minX) + padding * 2, (maxY - minY) + padding * 2);
        }

        public void Dispose()
        {
            _cachedLineGeometry?.Dispose();
            _cachedLineGeometry = null;
            _cachedArrowGeometry?.Dispose();
            _cachedArrowGeometry = null;
        }
    }

    private class GraphicsPolygon : IGraphicsShape
    {
        public PointF[] Points = [];
        private PathGeometry? _cachedGeometry;
        public void Render(RenderTarget renderTarget, Brush fillBrush, Brush strokeBrush, float strokeWidth)
        {
            if (Points.Length < 3) return;
            if (_cachedGeometry is null || _cachedGeometry.Factory.NativePointer != renderTarget.Factory.NativePointer)
            {
                Dispose();
                _cachedGeometry = BuildGeometry(renderTarget.Factory);
            }
            if (fillBrush is not null)
            {
                renderTarget.FillGeometry(_cachedGeometry, fillBrush);
            }
            if (strokeBrush is not null && strokeWidth > 0)
            {
                renderTarget.DrawGeometry(_cachedGeometry, strokeBrush, strokeWidth);
            }
        }
        private PathGeometry BuildGeometry(Factory factory)
        {
            var geometry = new PathGeometry(factory);
            using var sink = geometry.Open();
            sink.BeginFigure(new RawVector2(Points[0].X, Points[0].Y), FigureBegin.Filled);
            for (int i = 1; i < Points.Length; i++)
            {
                sink.AddLine(new RawVector2(Points[i].X, Points[i].Y));
            }
            sink.EndFigure(FigureEnd.Closed);
            sink.Close();
            return geometry;
        }
        public RectangleF GetBounds()
        {
            if (Points.Length == 0) return RectangleF.Empty;
            float minX = Points[0].X, minY = Points[0].Y, maxX = Points[0].X, maxY = Points[0].Y;
            foreach (var p in Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }
        public void Dispose()
        {
            _cachedGeometry?.Dispose();
            _cachedGeometry = null;
        }
    }

    #endregion

}