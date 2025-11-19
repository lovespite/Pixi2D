using Pixi2D.Core;
using SharpDX.Direct2D1;

namespace Pixi2D.Controls;

/// <summary>
/// 一个简单的旋转加载组件。
/// 显示一个 Sprite 并使其绕中心旋转，常用于表示“加载中”状态。
/// 调用 Dispose() 可停止旋转并从场景中移除。
/// </summary>
public class SpinLoading : Container
{
    private readonly Sprite _sprite;

    /// <summary>
    /// 旋转速度 (弧度/秒)。默认为 6.0 (约为 1 圈/秒)。
    /// </summary>
    public float Speed { get; set; } = 6.0f;

    /// <summary>
    /// 使用现有的 Sprite 创建 SpinLoading 组件。
    /// </summary>
    /// <param name="sprite">要旋转的 Sprite 对象。</param>
    public SpinLoading(Sprite sprite)
    {
        _sprite = sprite ?? throw new ArgumentNullException(nameof(sprite));

        // 关键：将锚点设置为中心 (0.5, 0.5)，以确保围绕自身中心旋转
        _sprite.SetAnchor(0.5f, 0.5f);

        // 将 Sprite 放置在容器的 (0,0) 位置
        _sprite.X = 0;
        _sprite.Y = 0;

        AddChild(_sprite);

        Width = _sprite.Width;
        Height = _sprite.Height;
    }

    /// <summary>
    /// 使用位图创建 SpinLoading 组件的便捷构造函数。
    /// </summary>
    /// <param name="bitmap">要显示的位图资源。</param>
    public SpinLoading(Bitmap1 bitmap) : this(new Sprite(bitmap))
    {
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
            _sprite.Rotation += Speed * deltaTime;

            // 保持旋转角度在 0 ~ 2PI 之间，防止长时间运行导致浮点数精度问题
            if (_sprite.Rotation > Math.PI * 2)
            {
                _sprite.Rotation -= (float)(Math.PI * 2);
            }
        }
    }

    // Dispose() 方法继承自 Container。
    // 当调用 Dispose() 时，它会执行以下操作：
    // 1. 从 Parent (父容器) 中移除自己 -> 这会导致 Update 不再被调用，从而停止旋转。
    // 2. 自动 Dispose 所有 Children (包括 _sprite)。
}