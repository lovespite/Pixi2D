using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pixi2D.Components.Utils;

public static class TouchKeyboardHelper
{
    // COM 类和接口定义
    [ComImport, Guid("4ce576fa-83dc-4F88-951c-9d0782b4e376")]
    private class UIHostNoLaunch { }

    [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITipInvocation
    {
        void Toggle(IntPtr hwnd);
    }

    [DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr GetDesktopWindow();

    /// <summary>
    /// 唤起或隐藏系统触控键盘 (带自动降级保护)
    /// </summary>
    public static void ToggleKeyboard(IntPtr hwnd)
    {
        // 方案 A：尝试通过优雅的 COM 接口唤起（支持切换显示/隐藏）
        try
        {
            var uiHostNoLaunch = new UIHostNoLaunch();
            var tipInvocation = (ITipInvocation)uiHostNoLaunch;

            IntPtr targetHwnd = hwnd != IntPtr.Zero ? hwnd : GetDesktopWindow();
            tipInvocation.Toggle(targetHwnd);

            Marshal.ReleaseComObject(uiHostNoLaunch);
            return; // 如果成功，直接返回
        }
        catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80040154))
        {
            System.Diagnostics.Debug.WriteLine("COM 接口未注册，正在尝试回退方案...");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"COM 调用触控键盘失败: {ex.Message}");
        }

        // 方案 B：降级回退到通过启动进程唤起
        FallbackLaunchTabTip();
    }

    private static void FallbackLaunchTabTip()
    {
        try
        {
            // 获取 TabTip.exe 的路径
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            string keyboardPath = Path.Combine(progFiles, @"Microsoft Shared\ink\TabTip.exe");

            if (File.Exists(keyboardPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = keyboardPath,
                    UseShellExecute = true
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("未找到 TabTip.exe，此系统可能不支持触控键盘。");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"回退启动 TabTip.exe 失败: {ex.Message}");
        }
    }
}