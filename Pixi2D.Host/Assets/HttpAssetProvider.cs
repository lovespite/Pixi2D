using System.Net.Http;

namespace Pixi2D.Host.Assets;

/// <summary>HTTP/HTTPS 资源加载。流式落盘到 <see cref="IAssetWriteSink"/>; 同时返回内存字节快照。</summary>
public sealed class HttpAssetProvider : IAssetProvider
{
    public HttpClient Client { get; }
    public AssetCachePolicy Policy { get; }

    /// <summary>请求生命周期事件 (供 NetworkHook 监听)。</summary>
    public event Action<Uri, string, IReadOnlyDictionary<string, string>>? RequestStart;
    public event Action<Uri, int, long, IReadOnlyDictionary<string, string>, TimeSpan>? RequestEnd;
    public event Action<Uri, string>? RequestError;

    public HttpAssetProvider(AssetCachePolicy policy)
    {
        Policy = policy;
        Client = new HttpClient { Timeout = policy.HttpTimeout };
        Client.DefaultRequestHeaders.UserAgent.ParseAdd(policy.HttpUserAgent);
    }

    public bool CanHandle(Uri uri) => uri.Scheme == "http" || uri.Scheme == "https";

    public async ValueTask<AssetData> LoadAsync(Uri uri, IAssetWriteSink? sink, Action<long, long>? progress, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyDictionary<string, string> reqHeaders = SnapshotHeaders(Client.DefaultRequestHeaders);
        RequestStart?.Invoke(uri, "GET", reqHeaders);

        try
        {
            using var resp = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var respHeaders = SnapshotHeaders(resp.Headers, resp.Content?.Headers);
            long total = resp.Content?.Headers.ContentLength ?? -1L;
            string? contentType = resp.Content?.Headers.ContentType?.MediaType;

            await using var net = await resp.Content!.ReadAsStreamAsync(ct).ConfigureAwait(false);

            // 双写: 内存 + 可选 sink
            using var mem = new MemoryStream(capacity: total > 0 && total < int.MaxValue ? (int)total : 64 * 1024);
            Stream? diskStream = sink?.Begin();
            try
            {
                var buf = new byte[64 * 1024];
                long readTotal = 0;
                int n;
                while ((n = await net.ReadAsync(buf.AsMemory(0, buf.Length), ct).ConfigureAwait(false)) > 0)
                {
                    mem.Write(buf, 0, n);
                    if (diskStream is not null) diskStream.Write(buf, 0, n);
                    readTotal += n;
                    if (total >= 1L * 1024 * 1024 || readTotal >= 1L * 1024 * 1024)
                        progress?.Invoke(readTotal, total);
                }

                diskStream?.Flush();
                diskStream?.Dispose();
                diskStream = null;
                sink?.Commit(contentType ?? "application/octet-stream", (int)resp.StatusCode, respHeaders);
            }
            catch
            {
                diskStream?.Dispose();
                sink?.Abort();
                throw;
            }

            sw.Stop();
            RequestEnd?.Invoke(uri, (int)resp.StatusCode, mem.Length, respHeaders, sw.Elapsed);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} for {uri}");

            return new AssetData(
                Source: uri,
                Bytes: mem.ToArray(),
                ContentType: contentType,
                FetchedAt: DateTimeOffset.UtcNow,
                FromCache: false,
                DiskPath: null,
                StatusCode: (int)resp.StatusCode,
                Headers: respHeaders);
        }
        catch (Exception ex)
        {
            sw.Stop();
            RequestError?.Invoke(uri, ex.Message);
            throw;
        }
    }

    private static IReadOnlyDictionary<string, string> SnapshotHeaders(System.Net.Http.Headers.HttpHeaders h, System.Net.Http.Headers.HttpHeaders? extra = null)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in h) d[kv.Key] = string.Join(", ", kv.Value);
        if (extra is not null) foreach (var kv in extra) d[kv.Key] = string.Join(", ", kv.Value);
        return d;
    }
}
