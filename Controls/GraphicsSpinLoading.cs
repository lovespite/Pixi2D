using Pixi2D.Core;
using SharpDX.Mathematics.Interop;
using System.Drawing;

namespace Pixi2D.Controls;

/// <summary>
/// 基于 Graphics 矢量图形的旋转加载组件。
/// 显示一个圆环或弧形图案并使其绕中心旋转，常用于表示"加载中"状态。
/// 调用 Dispose() 可停止旋转并从场景中移除。
/// </summary>
public class GraphicsSpinLoading : Container
{
    private readonly Graphics _graphics;

    /// <summary>
    /// 旋转速度 (弧度/秒)。默认为 6.0 (约为 1 圈/秒)。
    /// </summary>
    public float Speed { get; set; } = 6.0f;

    /// <summary>
    /// 加载器的半径。
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// 加载器的线条宽度。
    /// </summary>
    public float LineWidth { get; }

    /// <summary>
    /// 使用指定半径、颜色和线宽创建 GraphicsSpinLoading 组件。
    /// </summary>
    /// <param name="radius">加载器的半径。默认为 20。</param>
    /// <param name="color">加载器的颜色。默认为白色。</param>
    /// <param name="lineWidth">线条宽度。默认为 3。</param>
    public GraphicsSpinLoading(float radius = 20f, RawColor4? color = null, float lineWidth = 3f)
    {
        Radius = radius;
        LineWidth = lineWidth;

        _graphics = new Graphics
        {
            StrokeColor = color ?? new RawColor4(1f, 1f, 1f, 1f),
            StrokeWidth = lineWidth
        };

        // 绘制圆弧 (3/4 圆)
        DrawSpinner();

        // 将锚点设置为中心
        _graphics.SetAnchor(0.5f, 0.5f);

        // 将 Graphics 放置在容器的 (0,0) 位置
        _graphics.X = 0;
        _graphics.Y = 0;

        AddChild(_graphics);

        Width = radius * 2;
        Height = radius * 2;
    }

    /// <summary>
    /// 绘制旋转加载器的形状 (圆弧)。
    /// </summary>
    private void DrawSpinner()
    {
        // 绘制一个 270 度的圆弧 (3/4 圆)
        const int segments = 24; // 圆弧的分段数
        const float arcAngle = (float)(Math.PI * 1.5); // 270 度
        var points = new PointF[segments + 1];

        float centerX = Radius;
        float centerY = Radius;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * arcAngle;
            points[i] = new PointF
            {
                X = centerX + Radius * (float)Math.Cos(angle),
                Y = centerY + Radius * (float)Math.Sin(angle)
            };
        }

        _graphics.DrawCurve(points, 0f);
    }

    /// <summary>
    /// 每帧更新逻辑。
    /// </summary>
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        // 仅在可见时旋转
        if (Visible)
        {
            _graphics.Rotation += Speed * deltaTime;

            // 保持旋转角度在 0 ~ 2PI 之间，防止长时间运行导致浮点数精度问题
            if (_graphics.Rotation > Math.PI * 2)
            {
                _graphics.Rotation -= (float)(Math.PI * 2);
            }
        }
    }

    // Dispose() 方法继承自 Container。
    // 当调用 Dispose() 时，它会执行以下操作：
    // 1. 从 Parent (父容器) 中移除自己 -> 这会导致 Update 不再被调用，从而停止旋转。
    // 2. 自动 Dispose 所有 Children (包括 _graphics)。
}