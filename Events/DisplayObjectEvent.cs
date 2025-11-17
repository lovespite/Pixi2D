using Pixi2D;
using System.Drawing;

namespace Pixi2D.Events;

/// <summary>
/// Holds information about a display object event.
/// </summary>
public class DisplayObjectEvent
{
    /// <summary>
    /// The original DisplayObject that was the target of the event.
    /// </summary>
    public DisplayObject? Target { get; internal set; }

    /// <summary>
    /// The DisplayObject currently handling the event (during bubbling).
    /// </summary>
    public DisplayObject? CurrentTarget { get; internal set; }

    /// <summary>
    /// The mouse position in world (screen) coordinates.
    /// </summary>
    public PointF WorldPosition { get; internal set; }

    /// <summary>
    /// The mouse position in the Target's local coordinate system.
    /// </summary>
    public PointF LocalPosition { get; internal set; }

    public bool PropogationStopped { get; private set; }

    public DisplayObjectEventData? Data { get; internal set; }

    /// <summary>
    /// Stops the event from bubbling further up the display tree.
    /// </summary>
    public void StopPropagation()
    {
        PropogationStopped = true;
    }
}

public class DisplayObjectEventData
{
    public int Button { get; internal set; }
    public float MouseWheelDeltaY { get; internal set; }

    // --- Keyboard Data ---

    /// <summary>
    /// 键码 (例如 Keys.A)
    /// </summary>
    public int KeyCode { get; internal set; }

    /// <summary>
    /// KeyPress 事件的字符
    /// </summary>
    public char KeyChar { get; internal set; }

    // --- 修饰键 ---
    public bool Alt { get; internal set; }
    public bool Ctrl { get; internal set; }
    public bool Shift { get; internal set; }

    public object? AttachedObject { get; set; }
}