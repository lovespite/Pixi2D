using System.Runtime.InteropServices;
using System.Text;

namespace Pixi2D.Host;

/// <summary>
/// CLI 入口：<br />
/// <c>Pixi2D.Host.exe &lt;foo.pxml&gt; [--script foo.js] [--no-console] [--watch] [--width N] [--height N] [--title S]</c>
/// </summary>
internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        var opts = CliOptions.Parse(args);
        if (opts is null)
        {
            ShowUsage();
            return 2;
        }

        if (opts.UseConsole)
        {
            // 优先尝试附加到父控制台; 失败则 AllocConsole 启动一个独立窗口。
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
                AllocConsole();
            try { Console.OutputEncoding = Encoding.UTF8; } catch { /* ignore */ }
        }

        Console.WriteLine($"[host] PXML = {opts.PxmlPath}");
        if (opts.JsPath is not null) Console.WriteLine($"[host] JS   = {opts.JsPath}");

        try
        {
            using var window = new PixiHostWindow(
                opts.PxmlPath,
                opts.JsPath,
                opts.Watch,
                opts.Title,
                opts.Width,
                opts.Height);
            window.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[host] 致命错误:");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void ShowUsage()
    {
        Console.Error.WriteLine("用法: Pixi2D.Host.exe <foo.pxml> [选项]");
        Console.Error.WriteLine("选项:");
        Console.Error.WriteLine("  --script <foo.js>   显式指定 JS; 默认使用同名 .js");
        Console.Error.WriteLine("  --no-console        不分配控制台窗口");
        Console.Error.WriteLine("  --watch             文件变化时热重载");
        Console.Error.WriteLine("  --width  <N>        窗口宽度 (默认 1024)");
        Console.Error.WriteLine("  --height <N>        窗口高度 (默认 720)");
        Console.Error.WriteLine("  --title  <S>        窗口标题");
    }
}

internal sealed class CliOptions
{
    public required string PxmlPath { get; init; }
    public string? JsPath { get; init; }
    public bool UseConsole { get; init; } = true;
    public bool Watch { get; init; }
    public int Width { get; init; } = 1024;
    public int Height { get; init; } = 720;
    public string Title { get; init; } = "Pixi2D Host";

    public static CliOptions? Parse(string[] args)
    {
        if (args.Length == 0) return null;
        string? pxml = null, js = null, title = null;
        bool useConsole = true, watch = false;
        int width = 1024, height = 720;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--no-console": useConsole = false; break;
                case "--watch":      watch = true; break;
                case "--script":     js     = i + 1 < args.Length ? args[++i] : null; break;
                case "--width":      if (i + 1 < args.Length && int.TryParse(args[++i], out var w)) width = w; break;
                case "--height":     if (i + 1 < args.Length && int.TryParse(args[++i], out var h)) height = h; break;
                case "--title":      title  = i + 1 < args.Length ? args[++i] : null; break;
                default:
                    if (a.StartsWith("-", StringComparison.Ordinal)) return null;
                    pxml ??= a;
                    break;
            }
        }
        if (pxml is null || !File.Exists(pxml)) return null;

        return new CliOptions
        {
            PxmlPath = pxml,
            JsPath = js,
            UseConsole = useConsole,
            Watch = watch,
            Width = width,
            Height = height,
            Title = title ?? $"Pixi2D Host - {Path.GetFileName(pxml)}",
        };
    }
}
