using SharpDX.XAPO.Fx;
using System.Reflection.Metadata;

namespace Pixi2D.Components;


public class GeneralProgressMessage
{
    public string? Message { get; init; }
    public float Progress { get; init; } = 0f;
}

public class GeneralProgressBridge
{
    public event Action<GeneralProgressMessage>? OnProgress;
    public event Action? OnAbort;
    public event Action? OnCompleted;
    public event Action<Exception>? OnError;

    public bool IsAborted { get; private set; } = false;
    public bool IsCompleted { get; private set; } = false;
    public GeneralProgressMessage? LastMessage { get; private set; } = null;
    public Exception? LastError { get; private set; } = null;

    public void Abort()
    {
        if (IsCompleted || IsAborted) return;

        IsAborted = true;
        OnAbort?.Invoke();
    }

    public void Complete()
    {
        if (IsCompleted || IsAborted) return;

        IsCompleted = true;
        OnCompleted?.Invoke();
    }

    public void Report(GeneralProgressMessage msg)
    {
        LastMessage = msg;
        OnProgress?.Invoke(msg);
    }

    public void Report(Exception ex)
    {
        LastError = ex;
        OnError?.Invoke(ex);
    }
}