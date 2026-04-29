using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Pixi2D.Debugger.Connection;

/// <summary>JSON-line TCP 客户端 (与 Pixi2D.Host DebugBridge 配对)。</summary>
public sealed class DebugClient : IDisposable
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Channel<string>? _outbox;
    private int _nextId;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject?>> _pending = new();

    public bool IsConnected => _tcp?.Connected == true;

    /// <summary>每收到一帧时回调 (msgType, payload)。</summary>
    public event Action<string, JsonNode?>? OnFrame;
    public event Action? OnConnected;
    public event Action<string>? OnDisconnected;

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        Disconnect("reconnect");
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
        _stream = _tcp.GetStream();
        _outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => WriterLoopAsync(_stream, _outbox.Reader, _cts.Token));
        _ = Task.Run(() => ReaderLoopAsync(_stream, _cts.Token));

        OnConnected?.Invoke();
    }

    public void Send(string type, JsonObject? payload = null, int? id = null)
    {
        var box = _outbox;
        if (box is null) return;
        var frame = new JsonObject { ["type"] = type, ["payload"] = payload ?? new JsonObject() };
        if (id.HasValue) frame["id"] = id.Value;
        try { box.Writer.TryWrite(frame.ToJsonString()); } catch { }
    }

    /// <summary>发送 eval 并等待 evalResult。</summary>
    public Task<JsonObject?> EvalAsync(string code, TimeSpan timeout)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonObject?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        Send("eval", new JsonObject { ["code"] = code }, id);
        _ = Task.Delay(timeout).ContinueWith(_ =>
        {
            if (_pending.TryRemove(id, out var t)) t.TrySetResult(null);
        });
        return tcs.Task;
    }

    /// <summary>外部 (UI) 调用：用最近一次 evalResult 完成挂起的 eval。</summary>
    public void CompleteOldestEval(JsonObject result)
    {
        // 协议未带 id; 用 FIFO 完成最早挂起的请求
        if (_pending.IsEmpty) return;
        int min = int.MaxValue;
        foreach (var k in _pending.Keys) if (k < min) min = k;
        if (_pending.TryRemove(min, out var t)) t.TrySetResult(result);
    }

    private async Task WriterLoopAsync(NetworkStream stream, ChannelReader<string> reader, CancellationToken ct)
    {
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out var line))
                {
                    var bytes = Encoding.UTF8.GetBytes(line + "\n");
                    await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                }
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch { Disconnect("writer error"); }
    }

    private async Task ReaderLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[8192];
        var ms = new System.IO.MemoryStream();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buf, ct).ConfigureAwait(false);
                if (read <= 0) { Disconnect("eof"); return; }
                for (int i = 0; i < read; i++)
                {
                    if (buf[i] == (byte)'\n')
                    {
                        DispatchFrame(ms.ToArray());
                        ms.SetLength(0);
                    }
                    else ms.WriteByte(buf[i]);
                }
            }
        }
        catch (Exception ex) { Disconnect(ex.Message); }
    }

    private void DispatchFrame(byte[] line)
    {
        if (line.Length == 0) return;
        try
        {
            var node = JsonNode.Parse(line);
            if (node is null) return;
            string? type = node["type"]?.GetValue<string>();
            var payload = node["payload"];
            if (string.IsNullOrEmpty(type)) return;

            if (type == "evalResult" && payload is JsonObject po) CompleteOldestEval(po);
            OnFrame?.Invoke(type, payload);
        }
        catch { }
    }

    public void Disconnect(string reason)
    {
        try { _cts?.Cancel(); } catch { }
        try { _outbox?.Writer.TryComplete(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _tcp?.Close(); } catch { }
        _stream = null; _tcp = null; _outbox = null; _cts = null;
        OnDisconnected?.Invoke(reason);
    }

    public void Dispose() => Disconnect("dispose");
}
