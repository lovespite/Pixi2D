using Pixi2D.Events;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Pixi2D.Core;
/// <summary>
/// 一个可以包含其他 DisplayObject (包括其他 Container) 的容器。
/// 类似于 PIXI.js 中的 Container。
/// </summary>
public class Container : DisplayObject, IReadOnlyList<DisplayObject>
{
    static readonly RawRectangleF DefaultContentBounds = new(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);
    private readonly List<DisplayObject> _children = [];

    protected List<DisplayObject> Children => _children;

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

    public int Count => ((IReadOnlyCollection<DisplayObject>)_children).Count;

    public DisplayObject this[int index] => ((IReadOnlyList<DisplayObject>)_children)[index];

    // 缓存 RectangleGeometry 以避免每帧创建
    private RectangleGeometry? _cachedClipGeometry;
    private float _cachedClipWidth;
    private float _cachedClipHeight;

    public void AddChild(DisplayObject child)
    {
        child.Parent?.RemoveChild(child);
        child.Parent = this;
        Children.Add(child);
    }

    public void AddChildren(params DisplayObject[] newChildren)
    {
        foreach (var child in newChildren)
        {
            AddChild(child);
        }
    }

    public void RemoveChild(DisplayObject child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
        }
    }

    public void InsertChildAt(DisplayObject child, int index)
    {
        if (index < 0 || index > Children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
        }
        child.Parent?.RemoveChild(child);
        child.Parent = this;
        Children.Insert(index, child);
    }

    public void InsertBefore(DisplayObject child, DisplayObject? beforeChild)
    {
        int index = beforeChild is not null ? Children.IndexOf(beforeChild) : Children.Count;
        if (index == -1)
        {
            throw new ArgumentException("The specified beforeChild is not a child of this container.", nameof(beforeChild));
        }
        InsertChildAt(child, index);
    }

    public void InsertAfter(DisplayObject child, DisplayObject? afterChild)
    {
        int index = afterChild is not null ? Children.IndexOf(afterChild) : -1;
        if (index == -1 && afterChild is not null)
        {
            throw new ArgumentException("The specified afterChild is not a child of this container.", nameof(afterChild));
        }
        InsertChildAt(child, index + 1);
    }

    public void ClearChildren()
    {
        foreach (var child in Children)
        {
            child.Parent = null;
        }
        Children.Clear();
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        // 从后向前遍历，以便在更新期间安全地删除子项
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            Children[i].Update(deltaTime);
        }
    }

    internal override DisplayObject? FindHitObject(PointF worldPoint, Matrix3x2 currentTransform, DisplayObjectEvent hitEvent)
    {
        if (!Visible) return null; // 注意: Container 即使 Interactive=false 也要检查子项

        // 命中测试 (慢速路径): 总是重新计算变换
        Matrix3x2 myLocalTransform = CalculateLocalTransform();
        Matrix3x2 myWorldTransform = myLocalTransform * currentTransform;


        // 从后向前检查子项 (渲染在最上层的最先检查)
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var child = Children[i];

            // 递归检查
            DisplayObject? hitTarget = child.FindHitObject(worldPoint, myWorldTransform, hitEvent);

            if (hitTarget is not null)
            {
                // 在子项中找到了命中，立即返回
                return hitTarget;
            }
        }

        // 没有子项被命中。
        // 检查这个容器本身是否被命中 (这依赖于 base.FindHitObject 和 this.HitTest)
        // 只有当 this.Interactive == true 时才会检查
        return base.FindHitObject(worldPoint, myWorldTransform, hitEvent);
    }

    /// <summary>
    /// Container 本身是透明的，没有可命中的几何形状。
    /// 如果想让一个区域可点击，应在 Container 中添加一个 Graphics 子对象。
    /// </summary>
    public override bool HitTest(PointF localPoint)
    {
        return false;
    }

    private LayerParameters _layer;
    /// <summary>
    /// (已优化) 接受并传递 Matrix3x2。
    /// </summary>
    public override void Render(RenderTarget renderTarget, ref Matrix3x2 parentTransform)
    {
        if (!Visible) return;

        // 1. (优化) 计算或获取缓存的变换
        uint parentVersion = (Parent != null) ? Parent._worldVersion : 0;
        bool parentDirty = (parentVersion != _parentVersion);
        bool worldTransformUpdated = false;

        if (_localDirty || parentDirty)
        {
            // 只有在“脏”时才重新计算局部变换
            if (_localDirty)
            {
                _localTransform = CalculateLocalTransform();
                _localDirty = false;
            }

            // 重新计算世界变换
            _worldTransform = _localTransform * parentTransform;
            _parentVersion = parentVersion;
            _worldVersion++; // 我们的版本已更新
            _worldDirty = false;
            worldTransformUpdated = true;
        }
        else if (_worldDirty)
        {
            // 如果父级没变，我们也没变，但世界变换是脏的 (例如刚变为可见)
            // 仅使用缓存的 _localTransform 重建
            _worldTransform = _localTransform * parentTransform;
            _worldDirty = false;
            worldTransformUpdated = true;
        }
        // ... 否则, _worldTransform 已经是最新的，无需任何操作。

        // 2. 如果需要裁剪或透明度，使用图层
        bool layerPushed = false, needsRecreate = false;

        if (ClipContent || Alpha < 1.0f)
        {
            float clipW = ClipWidth ?? Width;
            float clipH = ClipHeight ?? Height;

            // 创建或重用几何遮罩用于裁剪
            if (ClipContent && clipW > 0 && clipH > 0)
            {
                var factory = renderTarget.Factory;

                // 检查是否需要重新创建几何体
                needsRecreate =
                                _cachedClipGeometry is null ||
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
                }
            }

            if (needsRecreate || worldTransformUpdated)
            {
                _layer = new LayerParameters
                {
                    ContentBounds = DefaultContentBounds,
                    GeometricMask = _cachedClipGeometry,
                    MaskTransform = Unsafe.As<Matrix3x2, RawMatrix3x2>(ref _worldTransform),
                    Opacity = Alpha
                };
            }

            renderTarget.PushLayer(ref _layer, null);
            layerPushed = true;
        }

        // 3. 递归渲染所有子项
        try
        {
            foreach (var child in Children.ToArray())
            {
                // (优化) 子项使用我们计算好的、缓存的 _worldTransform 来渲染
                child.Render(renderTarget, ref _worldTransform);
            }
        }
        finally
        {
            if (layerPushed)
            {
                renderTarget.PopLayer();
            }
        }
    }

    public T? FindChild<T>(string name, bool recursively = false) where T : DisplayObject
    {
        if (recursively) return FindChildRecursive<T>(name);

        foreach (var child in Children)
        {
            if (child.Name == name && child is T tChild)
            {
                return tChild;
            }
        }

        return null;
    }

    /// <summary>
    /// 递归查找子对象。默认为广度优先搜索。子类可根据需要修改为深度优先搜索(Stack)。
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected virtual T? FindChildRecursive<T>(string name) where T : DisplayObject
    {
        Container current;
        var stack = new Queue<Container>([this]);

        while (stack.Count > 0)
        {
            current = stack.Dequeue();
            foreach (var child in current.Children)
            {
                if (child.Name == name && child is T tChild)
                {
                    return tChild;
                }
                if (child is Container childContainer)
                {
                    stack.Enqueue(childContainer);
                }
            }
        }

        return null;
    }

    public override void Dispose()
    {
        base.Dispose();

        // 释放缓存的几何体
        _cachedClipGeometry?.Dispose();
        _cachedClipGeometry = null;
        // _cachedFactory = null;

        foreach (var child in Children.ToArray())
        {
            child.Dispose();
        }
        Children.Clear();
    }

    public IEnumerator<DisplayObject> GetEnumerator()
    {
        return ((IEnumerable<DisplayObject>)_children).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_children).GetEnumerator();
    }
}