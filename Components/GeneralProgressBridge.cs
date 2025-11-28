using SharpDX.XAPO.Fx;
using SharpDX.XAudio2;
using System.Reflection.Metadata;

namespace Pixi2D.Components;

public class GeneralProgressMessage
{
    public string? Message { get; init; }
    public float Progress { get; init; } = 0f;
}

public class GeneralProgressBridge: IDisposable
{
    public event Action<GeneralProgressMessage>? OnProgress;
    public event Action? OnAbort;
    public event Action? OnCompleted;
    public event Action<Exception>? OnError;

    private readonly CancellationTokenSource _cts = new();

    public CancellationToken CancellationToken => _cts.Token;
    public bool IsAborted => _cts.IsCancellationRequested;
    public bool IsCompleted { get; private set; } = false;
    public GeneralProgressMessage? LastMessage { get; private set; } = null;
    public Exception? LastError { get; private set; } = null;

    public GeneralProgressBridge()
    {
    }

    public void Abort()
    {
        if (IsCompleted || IsAborted) return;

        _cts.Cancel();
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

    public void Dispose()
    {
        _cts.Dispose();
    }
}