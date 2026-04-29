using Pixi2D.Host.Assets;
using QuickJsNet.Interop;

namespace Pixi2D.Host.Scripting;

/// <summary>
/// <c>globalThis.assets</c>：脚本资源加载入口。
/// </summary>
/// <remarks>
/// 设计：
///   • 异步方法 (load*) 立刻返回 int requestId; 加载结果通过事件投递 (与 obj.on('xxx', fn) 风格一致)。
///   • 同步方法 (*Sync / exists) 仅适合本地小文件; HTTP URL 走同步会拒绝并返回 null。
///   • bytes 跨边界传 base64 字符串 (避免 byte[] 桥接限制)。
///   • 所有事件回调通过 PixiHostWindow.RunOnUiThread 排队到主线程, 不会从 IO 线程直接进入 JS。
/// </remarks>
[JSExport]
public partial class AssetsProxy
{
    private readonly AssetLoader _loader;
    private readonly Action<Action> _toUi;

    /// <summary>文本加载完成 (requestId, url, text, metaJson)。</summary>
    public event Action<int, string, string, string>? LoadedText;
    /// <summary>二进制加载完成 (requestId, url, base64, metaJson)。</summary>
    public event Action<int, string, string, string>? LoadedBytes;
    /// <summary>JSON 加载完成 (requestId, url, jsonText, metaJson)；脚本侧自行 JSON.parse。</summary>
    public event Action<int, string, string, string>? LoadedJson;
    /// <summary>资源加载失败 (requestId, url, message)。</summary>
    public event Action<int, string, string>? Error;
    /// <summary>下载进度 (requestId, url, loaded, total); total &lt; 0 表示未知。仅 ≥1MB 文件触发。</summary>
    public event Action<int, string, long, long>? Progress;

    public AssetsProxy(AssetLoader loader, Action<Action> toUi)
    {
        _loader = loader;
        _toUi = toUi;
        _loader.Progress += (id, uri, loaded, total) => _toUi(() => Progress?.Invoke(id, uri.ToString(), loaded, total));
        _loader.Failed   += (id, uri, msg) => _toUi(() => Error?.Invoke(id, uri.ToString(), msg));
    }

    private string MetaToJson(AssetData d) =>
        $"{{\"source\":\"{Esc(d.Source.ToString())}\",\"contentType\":\"{Esc(d.ContentType ?? string.Empty)}\",\"fromCache\":{(d.FromCache ? "true" : "false")},\"fetchedAt\":\"{d.FetchedAt:O}\",\"sizeBytes\":{d.SizeBytes}{(d.StatusCode.HasValue ? $",\"statusCode\":{d.StatusCode.Value}" : string.Empty)}}}";

    private static string Esc(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                default:   sb.Append(c);  break;
            }
        }
        return sb.ToString();
    }

    // ---------- 异步 ----------

    /// <summary>异步加载文本；触发 loadedText / error。返回 requestId。</summary>
    public int LoadText(string url)
    {
        var task = _loader.LoadAsync(url, out var id, default);
        _ = AwaitAndDispatchAsync(task, id, kind: 0);
        return id;
    }

    /// <summary>异步加载字节 (base64);触发 loadedBytes / error。</summary>
    public int LoadBytes(string url)
    {
        var task = _loader.LoadAsync(url, out var id, default);
        _ = AwaitAndDispatchAsync(task, id, kind: 1);
        return id;
    }

    /// <summary>异步加载 JSON 文本;触发 loadedJson / error;脚本侧 JSON.parse。</summary>
    public int LoadJson(string url)
    {
        var task = _loader.LoadAsync(url, out var id, default);
        _ = AwaitAndDispatchAsync(task, id, kind: 2);
        return id;
    }

    private async Task AwaitAndDispatchAsync(ValueTask<AssetData> task, int id, int kind)
    {
        try
        {
            var data = await task.ConfigureAwait(false);
            var url = data.Source.ToString();
            var meta = MetaToJson(data);
            switch (kind)
            {
                case 0:
                {
                    var text = System.Text.Encoding.UTF8.GetString(data.Bytes);
                    _toUi(() => LoadedText?.Invoke(id, url, text, meta));
                    break;
                }
                case 1:
                {
                    var b64 = Convert.ToBase64String(data.Bytes);
                    _toUi(() => LoadedBytes?.Invoke(id, url, b64, meta));
                    break;
                }
                case 2:
                {
                    var text = System.Text.Encoding.UTF8.GetString(data.Bytes);
                    _toUi(() => LoadedJson?.Invoke(id, url, text, meta));
                    break;
                }
            }
        }
        catch
        {
            // Failed 事件已在 AssetLoader 内触发 → 这里吞掉避免未观察任务异常
        }
    }

    // ---------- 同步 ----------

    /// <summary>同步加载文本 (file:// 推荐); HTTP URL 返回 null。</summary>
    public string? LoadTextSync(string url)
    {
        try
        {
            var uri = _loader.Normalize(url);
            if (!uri.IsFile) return null;
            var data = _loader.LoadSync(url);
            return System.Text.Encoding.UTF8.GetString(data.Bytes);
        }
        catch { return null; }
    }

    /// <summary>同步加载字节 (返回 base64; file:// 推荐)。</summary>
    public string? LoadBytesSync(string url)
    {
        try
        {
            var uri = _loader.Normalize(url);
            if (!uri.IsFile) return null;
            var data = _loader.LoadSync(url);
            return Convert.ToBase64String(data.Bytes);
        }
        catch { return null; }
    }

    /// <summary>本地资源是否存在 (file:// 才有效)。</summary>
    public bool Exists(string url) => _loader.ExistsLocal(url);

    // ---------- 缓存控制 ----------

    /// <summary>清空全部缓存 (内存 + 磁盘)。</summary>
    public void ClearCache() => _loader.ClearCache();
    /// <summary>清空内存缓存。</summary>
    public void ClearMemoryCache() => _loader.ClearMemoryCache();
    /// <summary>清空磁盘缓存。</summary>
    public void ClearDiskCache() => _loader.ClearDiskCache();
    /// <summary>移除单条缓存 (内存 + 磁盘)。</summary>
    public void RemoveCache(string url) => _loader.RemoveCache(url);
    /// <summary>缓存统计 JSON 字符串: <c>{memoryBytes,memoryEntries,diskBytes,diskEntries}</c>。</summary>
    public string CacheStats()
    {
        var s = _loader.CacheStats();
        return $"{{\"memoryBytes\":{s.memoryBytes},\"memoryEntries\":{s.memoryEntries},\"diskBytes\":{s.diskBytes},\"diskEntries\":{s.diskEntries}}}";
    }
}
