namespace Pixi2D.Components;

public interface IClipboardProvider
{
    string? GetText();
    bool SetText(string text);
}
