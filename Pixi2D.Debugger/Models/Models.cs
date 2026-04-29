using System;
using System.Collections.ObjectModel;

namespace Pixi2D.Debugger.Models;

public sealed class TreeNodeVm
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public bool Visible { get; set; }
    public ObservableCollection<TreeNodeVm> Children { get; } = new();
    public string Display => string.IsNullOrEmpty(Name)
        ? $"{Kind} #{Id}  [{X:0},{Y:0} {W:0}x{H:0}]"
        : $"{Kind} \"{Name}\"  [{X:0},{Y:0} {W:0}x{H:0}]";
}

public sealed class ConsoleEntry
{
    public string Level { get; set; } = "log";
    public string Text { get; set; } = "";
    public DateTimeOffset Ts { get; set; } = DateTimeOffset.Now;
    public string Display => $"[{Ts:HH:mm:ss.fff}] {Level.ToUpperInvariant()}: {Text}";
}

public sealed class NetEntry
{
    public string Url { get; set; } = "";
    public string Method { get; set; } = "";
    public int Status { get; set; }
    public long Bytes { get; set; }
    public long Ms { get; set; }
    public string? Error { get; set; }
    public string Display => Error is not null
        ? $"ERR  {Url}  ({Error})"
        : $"{Status} {Method} {Url}  {Bytes}B {Ms}ms";
}

public sealed class FileEntry
{
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Size { get; set; }
    public DateTimeOffset Mtime { get; set; }
    public string Display => $"[{Kind}] {Path}  ({Size}B)";
}
