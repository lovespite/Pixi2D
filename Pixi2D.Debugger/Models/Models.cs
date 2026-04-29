using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Pixi2D.Debugger.Models;

public sealed class TreeNodeVm : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";

    private string? _name;     public string? Name { get => _name; set => Set(ref _name, value, nameof(Display)); }
    private double _x;         public double X { get => _x; set => Set(ref _x, value); }
    private double _y;         public double Y { get => _y; set => Set(ref _y, value); }
    private double _w;         public double W { get => _w; set => Set(ref _w, value); }
    private double _h;         public double H { get => _h; set => Set(ref _h, value); }
    private double _scaleX = 1;public double ScaleX { get => _scaleX; set => Set(ref _scaleX, value); }
    private double _scaleY = 1;public double ScaleY { get => _scaleY; set => Set(ref _scaleY, value); }
    private double _rotation;  public double Rotation { get => _rotation; set => Set(ref _rotation, value); }
    private double _alpha = 1; public double Alpha { get => _alpha; set => Set(ref _alpha, value); }
    private double _anchorX;   public double AnchorX { get => _anchorX; set => Set(ref _anchorX, value); }
    private double _anchorY;   public double AnchorY { get => _anchorY; set => Set(ref _anchorY, value); }
    private bool _visible = true;public bool Visible { get => _visible; set => Set(ref _visible, value); }
    private bool _interactive; public bool Interactive { get => _interactive; set => Set(ref _interactive, value); }
    private bool _acceptFocus; public bool AcceptFocus { get => _acceptFocus; set => Set(ref _acceptFocus, value); }

    public ObservableCollection<TreeNodeVm> Children { get; } = new();
    public string Display => string.IsNullOrEmpty(Name) ? $"{Kind} #{Id}" : $"{Kind} \"{Name}\" #{Id}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, string? alsoNotify = null, [CallerMemberName] string? prop = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        if (alsoNotify is not null) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(alsoNotify));
    }
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

public sealed class PropertyRow : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    /// <summary>"number" | "bool" | "string" | "readonly"</summary>
    public string Kind { get; set; } = "string";
    private string _value = "";
    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BoolValue)));
        }
    }
    public bool BoolValue
    {
        get => _value.Equals("true", StringComparison.OrdinalIgnoreCase);
        set => Value = value ? "true" : "false";
    }
    public bool IsBool => Kind == "bool";
    public bool IsEditable => Kind != "readonly";
    public bool IsTextEditable => IsEditable && !IsBool;
    public Visibility BoolVisibility => IsBool ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TextVisibility => IsTextEditable ? Visibility.Visible : Visibility.Collapsed;
    public event PropertyChangedEventHandler? PropertyChanged;
}
