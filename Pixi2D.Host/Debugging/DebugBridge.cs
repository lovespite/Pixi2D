using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Pixi2D.Host.Debugging;

/// <summary>
/// Host 侧调试桥：单连接 TCP + JSON-line 协议。
/// </summary>
/// <remarks>
/// 帧格式: <c>{"id"?:int, "type":string, "payload":object}\n</c><br/>
/// 仅监听 127.0.0.1。第二个客户端连接会被立即断开。<br/>
/// 调用 <see cref="Send(string,JsonObject)"/> 把事件入队 (通道); 后台 writer task 串行写出。<br/>
/// 入站请求通过 <see cref="OnRequest"/> 委托派发 (调用方负责切线程)。
/// </remarks>
public sealed class DebugBridge : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Channel<string>? _outbox;
    private Task? _acceptLoop;
    private Task? _writerLoop;
    private Task? _readerLoop;
    private int _nextId;

    public bool IsConnected => _client?.Connected == true;
    public int Port { get; }

    /// <summary>入站请求处理：返回 payload 作为响应；返回 null 不响应。</summary>
    public Func<string, JsonNode?, JsonNode?>? OnRequest { get; set; }
    public event Action? Connected;
    public event Action? Disconnected;

    public DebugBridge(int port = 9229)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start()
    {
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public void Send(string type, JsonObject? payload = null)
    {
        var box = _outbox;
        if (box is null) return;
        var frame = new JsonObject
        {
            ["type"] = type,
            ["payload"] = payload ?? new JsonObject(),
        };
        try { box.Writer.TryWrite(frame.ToJsonString()); } catch { }
    }

    public void SendWithId(int id, string type, JsonObject? payload = null)
    {
        var box = _outbox;
        if (box is null) return;
        var frame = new JsonObject
        {
            ["id"] = id,
            ["type"] = type,
            ["payload"] = payload ?? new JsonObject(),
        };
        try { box.Writer.TryWrite(frame.ToJsonString()); } catch { }
    }

    public int NextId() => System.Threading.Interlocked.Increment(ref _nextId);

    private async Task AcceptLoopAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            TcpClient? next = null;
            try { next = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch { return; }

            if (_client?.Connected == true)
            {
                try { next.Close(); } catch { }
                continue;
            }

            _client = next;
            _stream = next.GetStream();
            _outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

            _writerLoop = Task.Run(() => WriterLoopAsync(_stream, _outbox.Reader, ct));
            _readerLoop = Task.Run(() => ReaderLoopAsync(_stream, ct));

            Connected?.Invoke();
        }
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
        catch { }
        finally { TeardownConnection(); }
    }

    private async Task ReaderLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[8192];
        var ms = new MemoryStream();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buf, ct).ConfigureAwait(false);
                if (read <= 0) break;
                for (int i = 0; i < read; i++)
                {
                    if (buf[i] == (byte)'\n')
                    {
                        DispatchFrame(ms.ToArray());
                        ms.SetLength(0);
                    }
                    else
                    {
                        ms.WriteByte(buf[i]);
                    }
                }
            }
        }
        catch { }
        finally { TeardownConnection(); }
    }

    private void DispatchFrame(byte[] line)
    {
        if (line.Length == 0) return;
        try
        {
            var node = JsonNode.Parse(line);
            if (node is null) return;
            string? type = node["type"]?.GetValue<string>();
            int? id = node["id"]?.GetValue<int>();
            var payload = node["payload"];
            if (string.IsNullOrEmpty(type)) return;

            var resp = OnRequest?.Invoke(type, payload);
            if (resp is not null && id.HasValue)
            {
                var respObj = resp as JsonObject ?? new JsonObject { ["value"] = resp.DeepClone() };
                SendWithId(id.Value, type + ".reply", respObj);
            }
        }
        catch (Exception ex)
        {
            Send("error", new JsonObject { ["message"] = "frame parse: " + ex.Message });
        }
    }

    private void TeardownConnection()
    {
        var box = _outbox;
        _outbox = null;
        try { box?.Writer.TryComplete(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        _stream = null;
        _client = null;
        Disconnected?.Invoke();
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        TeardownConnection();
    }
}
