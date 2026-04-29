using System.Collections.Concurrent;

namespace Pixi2D.Host.Assets;

/// <summary>
/// 资源加载入口：URI 规范化、provider dispatch、调用 <see cref="AssetCache"/>、并发去重。
/// </summary>
public sealed class AssetLoader
{
    private readonly AssetCache _cache;
    private readonly List<IAssetProvider> _providers = new();
    private readonly ConcurrentDictionary<string, Task<AssetData>> _inflight = new(StringComparer.Ordinal);
    private int _nextRequestId;

    /// <summary>资源开始加载 (cache miss) → 实际触达 provider。</summary>
    public event Action<int, Uri>? Started;
    /// <summary>资源加载完成 (含 cache hit)。</summary>
    public event Action<int, AssetData>? Loaded;
    /// <summary>资源加载失败。</summary>
    public event Action<int, Uri, string>? Failed;
    /// <summary>下载进度 (≥1MB 文件才发；total &lt; 0 表示未知)。</summary>
    public event Action<int, Uri, long, long>? Progress;
    /// <summary>本地资源被加载 (供 FileTracker 登记)。</summary>
    public event Action<Uri, string>? LocalFileTouched;

    public AssetCache Cache => _cache;
    public string BaseDir { get; }
    public HttpAssetProvider Http { get; }
    public FileAssetProvider File { get; }

    public AssetLoader(string baseDir, AssetCachePolicy? policy = null)
    {
        BaseDir = Path.GetFullPath(baseDir);
        var p = policy ?? AssetCachePolicy.Default;
        _cache = new AssetCache(p);
        File = new FileAssetProvider(BaseDir);
        Http = new HttpAssetProvider(p);
        _providers.Add(File);
        _providers.Add(Http);
    }

    /// <summary>把字符串 URL/路径解析为绝对 URI。相对路径基于 <see cref="BaseDir"/>。</summary>
    public Uri Normalize(string urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath)) throw new ArgumentException("empty url");
        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var abs))
            return abs;
        var full = Path.GetFullPath(Path.Combine(BaseDir, urlOrPath));
        return new Uri(full);
    }

    /// <summary>同步检查本地存在 (file:// 才有效)。</summary>
    public bool ExistsLocal(string urlOrPath)
    {
        try
        {
            var uri = Normalize(urlOrPath);
            return uri.IsFile && System.IO.File.Exists(uri.LocalPath);
        }
        catch { return false; }
    }

    /// <summary>同步加载 (仅推荐 file://; HTTP 走此路径会阻塞)。</summary>
    public AssetData LoadSync(string urlOrPath)
    {
        var task = LoadAsync(urlOrPath, out var _, CancellationToken.None).AsTask();
        return task.GetAwaiter().GetResult();
    }

    /// <summary>异步加载；返回 task + requestId (用于事件关联)。</summary>
    public ValueTask<AssetData> LoadAsync(string urlOrPath, out int requestId, CancellationToken ct = default)
    {
        var uri = Normalize(urlOrPath);
        requestId = System.Threading.Interlocked.Increment(ref _nextRequestId);
        return new ValueTask<AssetData>(LoadCoreAsync(uri, requestId, ct));
    }

    private async Task<AssetData> LoadCoreAsync(Uri uri, int requestId, CancellationToken ct)
    {
        // 命中 L1
        var hit = _cache.TryGetMemory(uri);
        if (hit is not null)
        {
            if (hit.Source.IsFile) LocalFileTouched?.Invoke(hit.Source, hit.Source.LocalPath);
            Loaded?.Invoke(requestId, hit);
            return hit;
        }
        // 命中 L2
        var diskHit = _cache.TryGetDisk(uri);
        if (diskHit is not null)
        {
            _cache.PutMemory(diskHit);
            Loaded?.Invoke(requestId, diskHit);
            return diskHit;
        }

        // 并发去重 (按 URL key)
        var key = uri.ToString();
        var task = _inflight.GetOrAdd(key, _ => FetchAsync(uri, requestId, ct));
        try
        {
            var data = await task.ConfigureAwait(false);
            return data;
        }
        finally
        {
            _inflight.TryRemove(key, out _);
        }
    }

    private async Task<AssetData> FetchAsync(Uri uri, int requestId, CancellationToken ct)
    {
        var provider = _providers.FirstOrDefault(p => p.CanHandle(uri))
            ?? throw new NotSupportedException("No provider for scheme: " + uri.Scheme);

        Started?.Invoke(requestId, uri);

        IAssetWriteSink? sink = null;
        if (provider is HttpAssetProvider) sink = _cache.CreateSink(uri);

        try
        {
            var data = await provider.LoadAsync(uri, sink, (loaded, total) => Progress?.Invoke(requestId, uri, loaded, total), ct).ConfigureAwait(false);

            // 落盘条目: 把 DiskPath 修正为缓存路径
            if (sink is not null && data.DiskPath is null)
            {
                var disk = _cache.GetDiskPathForKey(uri);
                if (System.IO.File.Exists(disk))
                    data = data with { DiskPath = disk };
            }

            // 写入 L1 (本地文件 → 路径条目；HTTP 小文件 → 字节)
            _cache.PutMemory(data);

            if (uri.IsFile && data.DiskPath is not null)
                LocalFileTouched?.Invoke(uri, data.DiskPath);

            Loaded?.Invoke(requestId, data);
            return data;
        }
        catch (Exception ex)
        {
            Failed?.Invoke(requestId, uri, ex.Message);
            throw;
        }
    }

    public void ClearCache()      { _cache.ClearMemory(); _cache.ClearDisk(); }
    public void ClearMemoryCache(){ _cache.ClearMemory(); }
    public void ClearDiskCache()  { _cache.ClearDisk(); }
    public void RemoveCache(string urlOrPath)
    {
        try { _cache.Remove(Normalize(urlOrPath)); } catch { }
    }
    public (long memoryBytes, int memoryEntries, long diskBytes, int diskEntries) CacheStats() => _cache.Stats();
}
