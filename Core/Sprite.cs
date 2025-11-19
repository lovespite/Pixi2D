using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Pixi2D.Core;

// 使用 SharpDX.Direct2D1.Bitmap1 (来自 WIC) 或 SharpDX.Direct2D1.Bitmap (来自 DXGI)
// 为简单起见，这里我们假设是 Bitmap1
using D2DBitmap = Bitmap1;
/// <summary>
/// 用于显示位图 (纹理) 的 DisplayObject。
/// 类似于 PIXI.js 中的 Sprite。
/// </summary>
public class Sprite : DisplayObject
{
    /// <summary>
    /// 此 Sprite 要绘制的位图。
    /// </summary>
    public D2DBitmap? Bitmap { get; set; }

    /// <summary>
    /// 位图是否由此 Sprite “拥有”？
    /// 如果为 true, 则 Dispose() 将释放该位图。
    /// </summary>
    private readonly bool ownsBitmap;

    /// <summary>
    /// 创建一个新的 Sprite。
    /// </summary>
    /// <param name="bitmap">要显示的位图。</param>
    /// <param name="disposeBitmapWithSprite">
    /// 如果为 true, 则当此 Sprite 被 Dispose 时，位图也将被 Dispose。
    /// 如果位图在多个 Sprite 之间共享，则应将其设置为 false。
    /// </param>
    public Sprite(D2DBitmap bitmap, bool disposeBitmapWithSprite = false)
    {
        Bitmap = bitmap;
        ownsBitmap = disposeBitmapWithSprite;

        Width = bitmap.Size.Width;
        Height = bitmap.Size.Height;
    }

    /// <summary>
    /// 检查本地点是否在位图矩形内。
    /// </summary>
    public override bool HitTest(PointF localPoint)
    {
        if (Bitmap is null) return false;

        // 在本地坐标中的简单 AABB (轴对齐包围盒) 检查
        var size = Bitmap.Size;
        return localPoint.X >= 0 && localPoint.X < size.Width &&
               localPoint.Y >= 0 && localPoint.Y < size.Height;
    }

    /// <summary>
    /// (已优化) 接受 Matrix3x2。
    /// </summary>
    public override void Render(RenderTarget renderTarget, ref Matrix3x2 parentTransform)
    {
        if (!Visible || Bitmap is null) return;

        // 1. (优化) 计算或获取缓存的变换
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
        // ... 否则, _worldTransform 已经是最新的。


        // 2. 保存旧变换 (关键!)
        // renderTarget.Transform 返回 RawMatrix3x2
        var oldTransform = renderTarget.Transform;

        // 3. 设置变换
        // (优化) 使用缓存的 _worldTransform 
        renderTarget.Transform = Unsafe.As<Matrix3x2, RawMatrix3x2>(ref _worldTransform);

        // 4. 绘制 (使用支持 Alpha 的重载)
        // ... (绘制逻辑不变) ...
        var destRect = new RawRectangleF(0, 0, Bitmap.Size.Width, Bitmap.Size.Height);
        var sourceRect = new RawRectangleF(0, 0, Bitmap.Size.Width, Bitmap.Size.Height);

        renderTarget.DrawBitmap(
            Bitmap,
            destRect, // 目标矩形 (在我们的局部坐标系中)
            Alpha, // *这里* 我们应用 Sprite 自己的 Alpha
            BitmapInterpolationMode.Linear,
            sourceRect // 源矩形
        );

        // 5. 恢复变换 (关键!)
        renderTarget.Transform = oldTransform;
    }

    public override void Dispose()
    {
        base.Dispose();
        if (ownsBitmap)
        {
            Bitmap?.Dispose();
        }
        Bitmap = null;
    }
}