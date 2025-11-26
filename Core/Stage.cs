using Pixi2D.Components;
using Pixi2D.Controls;
using Pixi2D.Events;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Numerics;
namespace Pixi2D.Core;

/// <summary>
/// 场景的根容器。处理 BeginDraw/EndDraw。
/// </summary>
public sealed class Stage : Container
{
    private DisplayObject? _lastMouseOverObject = null;
    private DisplayObject? _lastMouseDownObject = null;

    private DisplayObject? _focusedObject = null;
    /// <summary>
    /// 获取当前拥有键盘焦点的对象
    /// </summary>
    public DisplayObject? FocusedObject => _focusedObject;

    private RenderTarget? _cachedRenderTarget;
    public IClipboardProvider? ClipboardProvider { get; set; }

    public bool SetClipboardText(string text)
    {
        if (ClipboardProvider is null) return false;
        return ClipboardProvider.SetText(text);
    }

    public string? GetClipboardText()
    {
        if (ClipboardProvider is null) return null;
        return ClipboardProvider.GetText();
    }

    public RenderTarget? GetCachedRenderTarget()
    {
        return _cachedRenderTarget;
    }

    public void SetCachedRenderTarget(RenderTarget? renderTarget)
    {
        Interlocked.Exchange(ref _cachedRenderTarget, renderTarget);
    }

    private Matrix3x2 Identity = Matrix3x2.Identity;

    public Stage()
    {
        Interactive = true;
        AcceptFocus = true;
    }

    /// <summary>
    /// 渲染整个场景。
    /// 此方法现在会触发变换更新和渲染。
    /// </summary>
    /// <param name="renderTarget">D2D 渲染目标。</param>
    public void Render(RenderTarget renderTarget)
    {
        Interlocked.CompareExchange(ref _cachedRenderTarget, renderTarget, null);

        // 1. (优化) 触发变换更新
        // 我们递增自己的版本号，这将强制所有子对象检查更新
        _worldVersion++;

        // 2. 以单位矩阵开始递归渲染 (Render 内部会处理变换更新)
        Render(renderTarget, ref Identity);
    }


    /// <summary>
    /// 获取指定显示对象在世界坐标系（Stage坐标系）下的轴对齐包围盒 (AABB)。
    /// </summary>
    /// <param name="target">目标显示对象。</param>
    /// <returns>世界坐标系下的包围盒 (x, y, width, height)。</returns>
    public static RectangleF GetObjectBounds(DisplayObject target)
    {
        if (target == null) return RectangleF.Empty;

        // 1. 获取目标对象最新的世界变换矩阵
        var transform = target.GetWorldTransform();

        // 2. 确定对象的本地包围盒 (Local Bounds)
        RectangleF localBounds;

        if (target is Graphics graphics)
        {
            // 对于 Graphics，使用其实际绘制内容的包围盒
            localBounds = graphics.GetBounds();
        }
        else if (target is Text text)
        {
            // 对于 Text，使用文本测量的尺寸
            var metrics = text.GetTextRect();
            localBounds = new RectangleF(metrics.Left, metrics.Top, metrics.Width, metrics.Height);
        }
        else
        {
            // 对于 Sprite, Container 或其他对象，默认使用 (0,0) 到 (Width,Height)
            // 注意：Anchor 的偏移已经在 CalculateLocalTransform 中处理了 (Translate(-Anchor))，
            // 所以这里的本地坐标系是以对象内容的左上角为 (0,0) 的。
            localBounds = new RectangleF(0, 0, target.Width, target.Height);
        }

        // 如果本地包围盒为空，直接返回空
        if (localBounds.Width == 0 && localBounds.Height == 0)
        {
            var pos = Vector2.Transform(new Vector2(localBounds.X, localBounds.Y), transform);
            return new RectangleF(pos.X, pos.Y, 0, 0);
        }

        // 3. 将本地包围盒的四个顶点变换到世界空间
        var p1 = Vector2.Transform(new Vector2(localBounds.Left, localBounds.Top), transform);     // 左上
        var p2 = Vector2.Transform(new Vector2(localBounds.Right, localBounds.Top), transform);    // 右上
        var p3 = Vector2.Transform(new Vector2(localBounds.Right, localBounds.Bottom), transform); // 右下
        var p4 = Vector2.Transform(new Vector2(localBounds.Left, localBounds.Bottom), transform);  // 左下

        // 4. 计算变换后的 AABB (Axis-Aligned Bounding Box)
        float minX = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X));
        float maxX = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X));
        float minY = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y));
        float maxY = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y));

        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    public void Resize(float newWidth, float newHeight)
    {
        this.Width = newWidth;
        this.Height = newHeight;
        OnResize?.Invoke(this, newWidth, newHeight);
    }

    public event Action<Stage, float, float>? OnResize;

    /// <summary>
    /// 设置当前拥有键盘焦点的对象。
    /// </summary>
    /// <param name="newFocus">希望获得焦点的对象，或 null 表示失去焦点。</param>
    public void SetFocus(DisplayObject? newFocus)
    {
        if (ReferenceEquals(_focusedObject, newFocus)) return;

        _focusedObject?.OnBlur?.Invoke();
        _focusedObject = newFocus;

        newFocus?.OnFocus?.Invoke();
    }

    /// <summary>
    /// 内部辅助方法，用于处理事件冒泡。
    /// </summary>
    /// <param name="target">事件的
    /// 原始目标。</param>
    /// <param name="worldPoint">世界坐标。</param>
    /// <param name="localPoint">目标的本地坐标。</param>
    /// <param name="eventSelector">一个委托，用于从 DisplayObject 中选择要调用的事件处理器。</param>
    private static void BubbleEvent(DisplayObject target, PointF worldPoint, PointF localPoint, DisplayObjectEventData? data, Func<DisplayObject, Action<DisplayObjectEvent>?> eventSelector)
    {
        var evt = new DisplayObjectEvent
        {
            Target = target,
            CurrentTarget = target,
            WorldPosition = worldPoint,
            LocalPosition = localPoint,
            Data = data,
        };

        var currentTarget = target;
        while (currentTarget is not null)
        {
            evt.CurrentTarget = currentTarget;

            // (例如, eventSelector 可能会返回 currentTarget.OnClick)
            Action<DisplayObjectEvent>? handler = eventSelector(currentTarget);
            handler?.Invoke(evt);

            if (evt.PropogationStopped) break; // 如果处理器调用了 StopPropagation()，则停止冒泡
            currentTarget = currentTarget.Parent;
        }
    }


    // --- 公共事件分发器 (由您的应用程序调用) ---

    #region Mouse Events
    /// <summary>
    /// 在鼠标移动时调用此方法。
    /// </summary>
    /// <param name="worldPoint">鼠标的屏幕/窗口坐标。</param>
    public void DispatchMouseMove(PointF worldPoint)
    {
        var evtData = new DisplayObjectEvent { WorldPosition = worldPoint };
        // 1. 查找被命中的对象 (HitTest 会按需计算变换)
        DisplayObject? hitObject = FindHitObject(worldPoint, Matrix3x2.Identity, evtData);

        // 2. 处理 MouseOver / MouseOut
        if (hitObject != _lastMouseOverObject)
        {
            // 鼠标移出了旧对象
            if (_lastMouseOverObject is not null)
            {
                var outEvt = new DisplayObjectEvent { Target = _lastMouseOverObject, CurrentTarget = _lastMouseOverObject, WorldPosition = worldPoint };
                _lastMouseOverObject.OnMouseOut?.Invoke(outEvt); // MouseOut 不冒泡
            }
            // 鼠标移入了新对象
            if (hitObject is not null)
            {
                var overEvt = new DisplayObjectEvent { Target = hitObject, CurrentTarget = hitObject, WorldPosition = worldPoint, LocalPosition = evtData.LocalPosition };
                hitObject.OnMouseOver?.Invoke(overEvt); // MouseOver 不冒泡
            }
            _lastMouseOverObject = hitObject;
        }

        BubbleEvent(hitObject ?? this, worldPoint, evtData.LocalPosition, null, (obj) => obj.OnMouseMove);
    }

    /// <summary>
    /// 在鼠标按下时调用此方法。
    /// </summary>
    public void DispatchMouseDown(PointF worldPoint, int button)
    {
        var evtData = new DisplayObjectEvent { WorldPosition = worldPoint };
        DisplayObject? hitObject = FindHitObject(worldPoint, Matrix3x2.Identity, evtData);

        // 如果点击了可交互对象，它将获得焦点。 
        if (hitObject is null)
        {
            SetFocus(this.FindFirstFocusableTarget());
        }
        else
        {
            SetFocus(hitObject.FindFirstFocusableTarget());
        }

        _lastMouseDownObject = hitObject; // 跟踪此对象，用于 "click" 检测

        BubbleEvent(hitObject ?? this, worldPoint, evtData.LocalPosition, new DisplayObjectEventData { Button = button }, (obj) => obj.OnMouseDown);
    }

    /// <summary>
    /// 在鼠标抬起时调用此方法。
    /// </summary>
    public void DispatchMouseUp(PointF worldPoint, int button)
    {
        var evtData = new DisplayObjectEvent { WorldPosition = worldPoint };
        DisplayObject? hitObject = FindHitObject(worldPoint, Matrix3x2.Identity, evtData);

        // 1. 触发 MouseUp (冒泡) 
        BubbleEvent(hitObject ?? this, worldPoint, evtData.LocalPosition, new DisplayObjectEventData { Button = button }, (obj) => obj.OnMouseUp);

        if (hitObject is not null && ReferenceEquals(hitObject, _lastMouseDownObject))
        {
            // 2. 触发 Click 事件 (冒泡)
            BubbleEvent(hitObject, worldPoint, evtData.LocalPosition, new DisplayObjectEventData { Button = button }, (obj) => obj.OnClick);
        }

        _lastMouseDownObject = null; // 重置
    }

    public void DispatchMouseWheel(PointF worldPoint, float deltaY)
    {
        var evtData = new DisplayObjectEvent { WorldPosition = worldPoint };
        DisplayObject? hitObject = FindHitObject(worldPoint, Matrix3x2.Identity, evtData);
        if (hitObject is not null)
        {
            // 创建一个新的事件对象，包含滚轮数据
            var wheelEvent = new DisplayObjectEvent
            {
                Target = hitObject,
                CurrentTarget = hitObject,
                WorldPosition = worldPoint,
                LocalPosition = evtData.LocalPosition,
            };
            // 冒泡触发 MouseWheel 事件
            BubbleEvent(hitObject, worldPoint, evtData.LocalPosition, new DisplayObjectEventData { MouseWheelDeltaY = deltaY }, (obj) => obj.OnMouseWheel);
        }
    }
    #endregion

    #region Keyboard Events
    /// <summary>
    /// 在按键按下时调用此方法。
    /// 事件将分派给当前拥有焦点的对象。
    /// </summary>
    /// <param name="keyCode">键码 (例如 Keys.A)</param>
    /// <param name="ctrl">Ctrl 是否按下</param>
    /// <param name="alt">Alt 是否按下</param>
    /// <param name="shift">Shift 是否按下</param>
    public void DispatchKeyDown(int keyCode, bool ctrl, bool alt, bool shift)
    {
        if (_focusedObject is null || _focusedObject.OnKeyDown is null) return;

        var evt = new DisplayObjectEvent
        {
            Target = _focusedObject,
            CurrentTarget = _focusedObject,
            Data = new DisplayObjectEventData
            {
                KeyCode = keyCode,
                Ctrl = ctrl,
                Alt = alt,
                Shift = shift
            }
        };
        _focusedObject.OnKeyDown(evt);
        // 键盘事件通常不冒泡
    }

    /// <summary>
    /// 在按键抬起时调用此方法。
    /// 事件将分派给当前拥有焦点的对象。
    /// </summary>
    public void DispatchKeyUp(int keyCode, bool ctrl, bool alt, bool shift)
    {
        if (_focusedObject is null || _focusedObject.OnKeyUp is null) return;

        var evt = new DisplayObjectEvent
        {
            Target = _focusedObject,
            CurrentTarget = _focusedObject,
            Data = new DisplayObjectEventData
            {
                KeyCode = keyCode,
                Ctrl = ctrl,
                Alt = alt,
                Shift = shift
            }
        };
        _focusedObject.OnKeyUp(evt);
    }

    /// <summary>
    /// 在输入字符时调用此方法。
    /// 事件将分派给当前拥有焦点的对象。
    /// </summary>
    /// <param name="keyChar">输入的字符</param>
    public void DispatchKeyPress(char keyChar)
    {
        if (_focusedObject is null || _focusedObject.OnKeyPress is null) return;

        var evt = new DisplayObjectEvent
        {
            Target = _focusedObject,
            CurrentTarget = _focusedObject,
            Data = new DisplayObjectEventData
            {
                KeyChar = keyChar
            }
        };
        _focusedObject.OnKeyPress(evt);
    }
    #endregion
}