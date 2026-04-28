using System.Runtime.InteropServices;

namespace Pixi2D.Host;

/// <summary>
/// Host 私有 Win32 P/Invoke 桥接：仅用 LibraryImport（AOT 友好），不依赖 System.Windows.Forms。
/// 目前只暴露 SetTimer / KillTimer，用于把 JS 事件循环的 pump 与 WM_TIMER 关联，
/// 这样在窗口拖动 / 缩放 / 模态消息循环期间 setInterval 等定时器仍能滴答。
/// </summary>
internal static partial class HostNative
{
    public const uint WM_TIMER = 0x0113;

    /// <summary>
    /// 注册一个绑定到指定窗口的计时器；超时后内核往该窗口的 WndProc 投递 WM_TIMER。
    /// 传 IntPtr.Zero 给 lpTimerFunc 表示走 WndProc 通路（而非回调函数），无 GC 钉扎压力。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "SetTimer")]
    public static partial nuint SetTimer(nint hWnd, nuint nIDEvent, uint uElapse, nint lpTimerFunc);

    [LibraryImport("user32.dll", EntryPoint = "KillTimer")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hWnd, nuint uIDEvent);
}
