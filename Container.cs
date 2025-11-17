using Pixi2D.Events;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;

namespace Pixi2D;
/// <summary>
/// 一个可以包含其他 DisplayObject (包括其他 Container) 的容器。
/// 类似于 PIXI.js 中的 Container。
/// </summary>
public class Container : DisplayObject
{
    protected readonly List<DisplayObject> children = [];

    /// <summary>
    /// 是否启用裁剪。如果为 true，超出裁剪区域的内容将被隐藏。
    /// </summary>
    public bool ClipContent { get; set; } = false;

    /// <summary>
    /// 裁剪区域的宽度。如果为 null，使用容器的 Width。
    /// </summary>
    public float? ClipWidth { get; set; } = null;

    /// <summary>
    /// 裁剪区域的高度。如果为 null，使用容器的 Height。
    /// </summary>
    public float? ClipHeight { get; set; } = null;

    // 缓存 RectangleGeometry 以避免每帧创建
    private RectangleGeometry? _cachedClipGeometry;
    private float _cachedClipWidth;
    private float _cachedClipHeight;
    // private SharpDX.Direct2D1.Factory? _cachedFactory;

    public void AddChild(DisplayObject child)
    {
        child.Parent?.RemoveChild(child);
        child.Parent = this;
        children.Add(child);
    }

    public void AddChildren(params DisplayObject[] newChildren)
    {
        foreach (var child in newChildren)
        {
            AddChild(child);
        }
    }

    public DisplayObject ReplaceChild(DisplayObject newChild, DisplayObject oldChild)
    {
        int index = children.IndexOf(oldChild);
        if (index == -1)
        {
            throw new ArgumentException("The specified oldChild is not a child of this container.", nameof(oldChild));
        }
        // 移除旧子项
        oldChild.Parent = null;
        // 添加新子项
        newChild.Parent?.RemoveChild(newChild);
        newChild.Parent = this;
        children[index] = newChild;
        return oldChild;
    }

    public void RemoveChild(DisplayObject child)
    {
        if (children.Remove(child))
        {
            child.Parent = null;
        }
    }

    public void InsertChildAt(DisplayObject child, int index)
    {
        if (index < 0 || index > children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
        }
        child.Parent?.RemoveChild(child);
        child.Parent = this;
        children.Insert(index, child);
    }

    public void InsertBefore(DisplayObject child, DisplayObject? beforeChild)
    {
        int index = beforeChild != null ? children.IndexOf(beforeChild) : children.Count;
        if (index == -1)
        {
            throw new ArgumentException("The specified beforeChild is not a child of this container.", nameof(beforeChild));
        }
        InsertChildAt(child, index);
    }

    public void InsertAfter(DisplayObject child, DisplayObject? afterChild)
    {
        int index = afterChild != null ? children.IndexOf(afterChild) : -1;
        if (index == -1 && afterChild != null)
        {
            throw new ArgumentException("The specified afterChild is not a child of this container.", nameof(afterChild));
        }
        InsertChildAt(child, index + 1);
    }

    public void ExchangeChildren(DisplayObject childA, DisplayObject childB)
    {
        int indexA = children.IndexOf(childA);
        int indexB = children.IndexOf(childB);
        if (indexA == -1 || indexB == -1)
        {
            throw new ArgumentException("Both children must be direct children of this container.");
        }
        children[indexA] = childB;
        children[indexB] = childA;
    }

    public void ClearChildren()
    {
        foreach (var child in children)
        {
            child.Parent = null;
        }
        children.Clear();
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        // 从后向前遍历，以便在更新期间安全地删除子项
        for (int i = children.Count - 1; i >= 0; i--)
        {
            children[i].Update(deltaTime);
        }
    }

    internal override DisplayObject? FindHitObject(PointF worldPoint, Matrix3x2 currentTransform, DisplayObjectEvent hitEvent)
    {
        if (!Visible) return null; // 注意: Container 即使 Interactive=false 也要检查子项

        // 从后向前检查子项 (渲染在最上层的最先检查)
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];

            // 计算子项的世界变换
            Matrix3x2 childWorldTransform = child.GetLocalTransform() * currentTransform;

            // 递归检查
            DisplayObject? hitTarget = child.FindHitObject(worldPoint, childWorldTransform, hitEvent);

            if (hitTarget != null)
            {
                // 在子项中找到了命中，立即返回
                return hitTarget;
            }
        }

        // 没有子项被命中。
        // 检查这个容器本身是否被命中 (这依赖于 base.FindHitObject 和 this.HitTest)
        // 只有当 this.Interactive == true 时才会检查
        return base.FindHitObject(worldPoint, currentTransform, hitEvent);
    }

    /// <summary>
    /// Container 本身是透明的，没有可命中的几何形状。
    /// 如果想让一个区域可点击，应在 Container 中添加一个 Graphics 子对象。
    /// </summary>
    public override bool HitTest(PointF localPoint)
    {
        return false;
    }

    /// <summary>
    /// 已更改: 接受并传递 Matrix3x2。
    /// </summary>
    public override void Render(RenderTarget renderTarget, Matrix3x2 parentTransform)
    {
        if (!Visible) return;

        // 1. 计算我们自己的变换
        Matrix3x2 myLocalTransform = GetLocalTransform();
        Matrix3x2 myWorldTransform = myLocalTransform * parentTransform;

        // 2. 如果需要裁剪或透明度，使用图层
        bool layerPushed = false;

        if (ClipContent || Alpha < 1.0f)
        {
            float clipW = ClipWidth ?? Width;
            float clipH = ClipHeight ?? Height;

            // 创建或重用几何遮罩用于裁剪
            RectangleGeometry? clipGeometry = null;
            if (ClipContent && clipW > 0 && clipH > 0)
            {
                var factory = renderTarget.Factory;

                // 检查是否需要重新创建几何体
                bool needsRecreate =
                                    _cachedClipGeometry == null ||
                                    // _cachedFactory != factory ||
                                    _cachedClipWidth != clipW ||
                                    _cachedClipHeight != clipH;

                if (needsRecreate)
                {
                    // 释放旧的几何体
                    _cachedClipGeometry?.Dispose();

                    // 创建新的几何体
                    _cachedClipGeometry = new RectangleGeometry(factory, new RawRectangleF(0, 0, clipW, clipH));
                    _cachedClipWidth = clipW;
                    _cachedClipHeight = clipH;
                    // _cachedFactory = factory;
                }

                clipGeometry = _cachedClipGeometry;
            }

            var layerParameters = new LayerParameters
            {
                ContentBounds = new RawRectangleF(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue),
                GeometricMask = clipGeometry,
                MaskTransform = new RawMatrix3x2
                {
                    M11 = myWorldTransform.M11,
                    M12 = myWorldTransform.M12,
                    M21 = myWorldTransform.M21,
                    M22 = myWorldTransform.M22,
                    M31 = myWorldTransform.M31,
                    M32 = myWorldTransform.M32
                },
                Opacity = Alpha
            };
            renderTarget.PushLayer(ref layerParameters, null);
            layerPushed = true;
        }

        // 3. 递归渲染所有子项
        try
        {
            foreach (var child in children)
            {
                // 子项使用计算好的完整世界变换来渲染
                child.Render(renderTarget, myWorldTransform);
            }
        }
        finally
        {
            if (layerPushed)
            {
                renderTarget.PopLayer();
            }
            // 注意: 不再在这里 Dispose clipGeometry，因为它现在被缓存了
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        // 释放缓存的几何体
        _cachedClipGeometry?.Dispose();
        _cachedClipGeometry = null;
        // _cachedFactory = null;

        foreach (var child in children.ToArray())
        {
            child.Dispose();
        }
        children.Clear();
    }
}