using System.Text.Json.Nodes;
using Pixi2D.Core;
using Pixi2D.Host.Assets;
using Pixi2D.Scripting.QuickJs;

namespace Pixi2D.Host.Debugging;

/// <summary>
/// 把 DebugBridge 与 PixiHostWindow 上的 ConsoleLog / AssetLoader 网络事件 / FileTracker / TreeSerializer / EvalHandler 串起来。
/// </summary>
public sealed class DebugHost : IDisposable
{
    private readonly DebugBridge _bridge;
    private readonly Stage _stage;
    private readonly QuickJsScriptEngine _engine;
    private readonly AssetLoader? _assets;
    private readonly Action<Action> _toUi;
    private readonly string _pxmlPath;

    /// <summary>已打开文件登记表 (path → kind)。</summary>
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    private System.Threading.Timer? _treeTimer;

    public DebugHost(int port, Stage stage, QuickJsScriptEngine engine, AssetLoader? assets, Action<Action> toUi, string pxmlPath, string? jsPath)
    {
        _bridge = new DebugBridge(port);
        _stage  = stage;
        _engine = engine;
        _assets = assets;
        _toUi   = toUi;
        _pxmlPath = pxmlPath;

        // 初始登记 PXML / JS
        TrackFile(pxmlPath, "pxml");
        if (!string.IsNullOrEmpty(jsPath)) TrackFile(jsPath!, "js");

        // ConsoleHook：转发 OnLog → bridge
        _engine.OnLog += (level, msg) =>
        {
            _bridge.Send("console", new JsonObject
            {
                ["level"] = level switch { 1 => "warn", 2 => "error", _ => "log" },
                ["text"]  = msg ?? string.Empty,
                ["ts"]    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        };

        // NetworkHook + FileTracker：从 AssetLoader 事件订阅
        if (_assets is not null)
        {
            _assets.Http.RequestStart += (uri, method, headers) =>
            {
                _bridge.Send("network", new JsonObject
                {
                    ["phase"]   = "start",
                    ["url"]     = uri.ToString(),
                    ["method"]  = method,
                    ["headers"] = HeadersToJson(headers),
                    ["ts"]      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                });
            };
            _assets.Http.RequestEnd += (uri, status, bytes, headers, elapsed) =>
            {
                _bridge.Send("network", new JsonObject
                {
                    ["phase"]   = "end",
                    ["url"]     = uri.ToString(),
                    ["status"]  = status,
                    ["bytes"]   = bytes,
                    ["headers"] = HeadersToJson(headers),
                    ["ms"]      = (long)elapsed.TotalMilliseconds,
                    ["ts"]      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                });
            };
            _assets.Http.RequestError += (uri, msg) =>
            {
                _bridge.Send("network", new JsonObject
                {
                    ["phase"] = "error",
                    ["url"]   = uri.ToString(),
                    ["error"] = msg,
                    ["ts"]    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                });
            };
            _assets.LocalFileTouched += (uri, path) => TrackFile(path, "asset");
        }

        // EvalHandler & 协议入站
        _bridge.OnRequest = (type, payload) => HandleRequest(type, payload);

        _bridge.Connected += OnClientConnected;
        _bridge.Disconnected += () => { try { _treeTimer?.Dispose(); } catch { } _treeTimer = null; };
    }

    public int Port => _bridge.Port;
    public bool IsConnected => _bridge.IsConnected;

    public void Start() => _bridge.Start();

    private void TrackFile(string path, string kind)
    {
        try { path = Path.GetFullPath(path); } catch { }
        bool changed;
        lock (_files)
        {
            changed = !_files.ContainsKey(path);
            _files[path] = kind;
        }
        if (changed)
        {
            _bridge.Send("file", new JsonObject
            {
                ["path"]  = path,
                ["kind"]  = kind,
                ["size"]  = SafeFileSize(path),
                ["mtime"] = SafeMtime(path),
            });
        }
    }

    private static long SafeFileSize(string p) { try { return new FileInfo(p).Length; } catch { return -1; } }
    private static long SafeMtime(string p)    { try { return new DateTimeOffset(File.GetLastWriteTimeUtc(p)).ToUnixTimeMilliseconds(); } catch { return 0; } }

    private static JsonObject HeadersToJson(IReadOnlyDictionary<string, string> headers)
    {
        var o = new JsonObject();
        foreach (var (k, v) in headers) o[k] = v;
        return o;
    }

    private void OnClientConnected()
    {
        // 握手
        _bridge.Send("hello", new JsonObject
        {
            ["host"]     = "Pixi2D.Host",
            ["version"]  = "0.7.0",
            ["pxmlPath"] = _pxmlPath,
            ["pid"]      = Environment.ProcessId,
        });

        // 全量树初推
        PushTree();
        // 全量已打开文件初推
        lock (_files)
        {
            foreach (var (p, k) in _files)
            {
                _bridge.Send("file", new JsonObject
                {
                    ["path"]  = p,
                    ["kind"]  = k,
                    ["size"]  = SafeFileSize(p),
                    ["mtime"] = SafeMtime(p),
                });
            }
        }

        // 周期推送树 (1s)
        _treeTimer = new System.Threading.Timer(_ => _toUi(PushTree), null, 1000, 1000);
    }

    private void PushTree()
    {
        try
        {
            var json = TreeSerializer.Serialize(_stage);
            _bridge.Send("tree.update", json);
        }
        catch (Exception ex)
        {
            _bridge.Send("error", new JsonObject { ["message"] = "tree: " + ex.Message });
        }
    }

    private JsonNode? HandleRequest(string type, JsonNode? payload)
    {
        switch (type)
        {
            case "tree.refresh":
                _toUi(PushTree);
                return new JsonObject { ["ok"] = true };
            case "eval":
            {
                var code = payload?["code"]?.GetValue<string>() ?? string.Empty;
                EvalAsync(code);
                return null; // result will arrive via 'evalResult'
            }
        }
        return null;
    }

    private void EvalAsync(string code)
    {
        _toUi(() =>
        {
            var resp = new JsonObject();
            try
            {
                var result = _engine.Inner.Eval(code, "<repl>", asModule: false);
                resp["ok"] = true;
                resp["value"] = result?.ToString() ?? "undefined";
            }
            catch (Exception ex)
            {
                resp["ok"] = false;
                resp["error"] = ex.Message;
            }
            _bridge.Send("evalResult", resp);
        });
    }

    public void Dispose()
    {
        try { _treeTimer?.Dispose(); } catch { }
        _bridge.Dispose();
    }
}
