using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pixi2D.Host.Assets;

/// <summary>
/// 分级缓存：
///   L1 内存 LRU：仅缓存 &lt; <see cref="AssetCachePolicy.MemoryThresholdBytes"/> 的资源；
///                本地文件只放路径条目 (不持有字节, 按需读盘)。
///   L2 磁盘 tmp/：所有 HTTP 资源 + 任何 ≥1MB 资源；按 URL hash 命名 + sidecar .meta。
/// </summary>
public sealed class AssetCache
{
    private readonly AssetCachePolicy _policy;
    private readonly LinkedList<MemoryEntry> _lruList = new();
    private readonly Dictionary<string, LinkedListNode<MemoryEntry>> _lruIndex = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _memoryBytes;

    public AssetCache(AssetCachePolicy policy)
    {
        _policy = policy;
        try { Directory.CreateDirectory(_policy.DiskCacheDir); }
        catch { /* ignore */ }
    }

    public AssetCachePolicy Policy => _policy;

    // ---------- L1 内存 ----------
    private sealed class MemoryEntry
    {
        public required string Key;
        public required Uri Source;
        public byte[]? Bytes;             // null = 仅路径条目
        public string? DiskPath;
        public string? ContentType;
        public DateTimeOffset FetchedAt;
        public int? StatusCode;
        public IReadOnlyDictionary<string, string>? Headers;
        public long ApproxBytes;          // 用于 LRU 字节统计 (路径条目近似 64)
    }

    /// <summary>L1 命中：返回字节 + meta；若仅路径条目, 自动读盘。Miss 返回 null。</summary>
    public AssetData? TryGetMemory(Uri uri)
    {
        var key = uri.ToString();
        lock (_gate)
        {
            if (!_lruIndex.TryGetValue(key, out var node)) return null;
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            var e = node.Value;

            byte[] bytes;
            if (e.Bytes is not null) bytes = e.Bytes;
            else if (e.DiskPath is not null && File.Exists(e.DiskPath))
            {
                try { bytes = File.ReadAllBytes(e.DiskPath); }
                catch { return null; }
            }
            else return null;

            return new AssetData(
                Source: e.Source,
                Bytes: bytes,
                ContentType: e.ContentType,
                FetchedAt: e.FetchedAt,
                FromCache: true,
                DiskPath: e.DiskPath,
                StatusCode: e.StatusCode,
                Headers: e.Headers);
        }
    }

    /// <summary>把已加载的资源写入 L1。本地文件 (DiskPath 已存在) 自动只放路径条目, 不存字节。</summary>
    public void PutMemory(AssetData data)
    {
        var key = data.Source.ToString();
        bool localFileEntry = data.DiskPath is not null && data.Source.IsFile;
        bool inlineBytes = !localFileEntry && data.SizeBytes < _policy.MemoryThresholdBytes;

        lock (_gate)
        {
            if (_lruIndex.TryGetValue(key, out var existing))
            {
                _memoryBytes -= existing.Value.ApproxBytes;
                _lruList.Remove(existing);
                _lruIndex.Remove(key);
            }

            var entry = new MemoryEntry
            {
                Key = key,
                Source = data.Source,
                Bytes = inlineBytes ? data.Bytes : null,
                DiskPath = data.DiskPath,
                ContentType = data.ContentType,
                FetchedAt = data.FetchedAt,
                StatusCode = data.StatusCode,
                Headers = data.Headers,
                ApproxBytes = inlineBytes ? data.SizeBytes + 256 : 64,
            };
            var node = new LinkedListNode<MemoryEntry>(entry);
            _lruList.AddFirst(node);
            _lruIndex[key] = node;
            _memoryBytes += entry.ApproxBytes;

            EvictMemoryIfNeeded();
        }
    }

    private void EvictMemoryIfNeeded()
    {
        while ((_lruIndex.Count > _policy.MemoryMaxEntries || _memoryBytes > _policy.MemoryMaxBytes)
               && _lruList.Last is not null)
        {
            var tail = _lruList.Last;
            _lruList.RemoveLast();
            _lruIndex.Remove(tail.Value.Key);
            _memoryBytes -= tail.Value.ApproxBytes;
        }
    }

    // ---------- L2 磁盘 ----------

    /// <summary>计算磁盘缓存条目的文件路径 (无 sidecar)。</summary>
    public string GetDiskPathForKey(Uri uri)
    {
        var hash = HashKey(uri.ToString());
        return Path.Combine(_policy.DiskCacheDir, hash + ".bin");
    }

    private string GetMetaPath(string binPath) => binPath + ".meta";

    /// <summary>L2 命中：读 .bin + .meta；返回 AssetData (FromCache=true)。Miss 返回 null。</summary>
    public AssetData? TryGetDisk(Uri uri)
    {
        var bin = GetDiskPathForKey(uri);
        var meta = GetMetaPath(bin);
        if (!File.Exists(bin) || !File.Exists(meta)) return null;
        try
        {
            var metaJson = File.ReadAllText(meta);
            using var doc = JsonDocument.Parse(metaJson);
            var root = doc.RootElement;
            string? ct = root.TryGetProperty("contentType", out var ctEl) && ctEl.ValueKind == JsonValueKind.String ? ctEl.GetString() : null;
            int? sc = root.TryGetProperty("statusCode", out var scEl) && scEl.ValueKind == JsonValueKind.Number ? scEl.GetInt32() : null;
            DateTimeOffset fetched = root.TryGetProperty("fetchedAt", out var faEl) && faEl.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(faEl.GetString(), out var f) ? f : DateTimeOffset.UtcNow;
            IReadOnlyDictionary<string, string>? headers = null;
            if (root.TryGetProperty("headers", out var hEl) && hEl.ValueKind == JsonValueKind.Object)
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in hEl.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.String) d[p.Name] = p.Value.GetString() ?? string.Empty;
                headers = d;
            }
            var bytes = File.ReadAllBytes(bin);
            try { File.SetLastAccessTimeUtc(bin, DateTime.UtcNow); } catch { }
            return new AssetData(uri, bytes, ct, fetched, FromCache: true, bin, sc, headers);
        }
        catch { return null; }
    }

    /// <summary>新建一个流式写盘 sink (用于 HttpAssetProvider 边下边写)。</summary>
    public IAssetWriteSink CreateSink(Uri uri)
    {
        var bin = GetDiskPathForKey(uri);
        return new FileSink(this, uri, bin);
    }

    private void WriteMeta(string binPath, Uri uri, string? contentType, int? statusCode, IReadOnlyDictionary<string, string>? headers, DateTimeOffset fetchedAt)
    {
        try
        {
            using var fs = File.Create(GetMetaPath(binPath));
            using var w = new Utf8JsonWriter(fs);
            w.WriteStartObject();
            w.WriteString("source", uri.ToString());
            if (contentType is not null) w.WriteString("contentType", contentType);
            if (statusCode.HasValue) w.WriteNumber("statusCode", statusCode.Value);
            w.WriteString("fetchedAt", fetchedAt.ToString("O"));
            if (headers is not null)
            {
                w.WritePropertyName("headers");
                w.WriteStartObject();
                foreach (var kv in headers) w.WriteString(kv.Key, kv.Value);
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        catch { /* ignore */ }
    }

    /// <summary>磁盘 LRU 淘汰 (按 .meta 的最后访问时间)。</summary>
    public void EnforceDiskBudget()
    {
        try
        {
            var dir = new DirectoryInfo(_policy.DiskCacheDir);
            if (!dir.Exists) return;
            var bins = dir.GetFiles("*.bin");
            long total = 0;
            foreach (var f in bins) total += f.Length;
            if (total <= _policy.DiskMaxBytes) return;

            Array.Sort(bins, (a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
            int i = 0;
            while (total > _policy.DiskMaxBytes && i < bins.Length)
            {
                long sz = bins[i].Length;
                try { bins[i].Delete(); File.Delete(GetMetaPath(bins[i].FullName)); total -= sz; }
                catch { }
                i++;
            }
        }
        catch { }
    }

    // ---------- 维护 ----------

    public void ClearMemory()
    {
        lock (_gate)
        {
            _lruList.Clear();
            _lruIndex.Clear();
            _memoryBytes = 0;
        }
    }

    public void ClearDisk()
    {
        try
        {
            if (!Directory.Exists(_policy.DiskCacheDir)) return;
            foreach (var f in Directory.EnumerateFiles(_policy.DiskCacheDir, "*.bin"))   try { File.Delete(f); } catch { }
            foreach (var f in Directory.EnumerateFiles(_policy.DiskCacheDir, "*.meta"))  try { File.Delete(f); } catch { }
        }
        catch { }
    }

    public void Remove(Uri uri)
    {
        var key = uri.ToString();
        lock (_gate)
        {
            if (_lruIndex.TryGetValue(key, out var node))
            {
                _memoryBytes -= node.Value.ApproxBytes;
                _lruList.Remove(node);
                _lruIndex.Remove(key);
            }
        }
        var bin = GetDiskPathForKey(uri);
        try { if (File.Exists(bin)) File.Delete(bin); } catch { }
        try { if (File.Exists(GetMetaPath(bin))) File.Delete(GetMetaPath(bin)); } catch { }
    }

    public (long memoryBytes, int memoryEntries, long diskBytes, int diskEntries) Stats()
    {
        long memBytes; int memEntries;
        lock (_gate) { memBytes = _memoryBytes; memEntries = _lruIndex.Count; }
        long dBytes = 0; int dEntries = 0;
        try
        {
            var dir = new DirectoryInfo(_policy.DiskCacheDir);
            if (dir.Exists)
                foreach (var f in dir.GetFiles("*.bin")) { dBytes += f.Length; dEntries++; }
        }
        catch { }
        return (memBytes, memEntries, dBytes, dEntries);
    }

    private static string HashKey(string s)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(40);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    // ---------- Sink 实现 ----------
    private sealed class FileSink : IAssetWriteSink
    {
        private readonly AssetCache _owner;
        private readonly Uri _uri;
        private readonly string _binPath;
        private string? _tmpPath;

        public FileSink(AssetCache owner, Uri uri, string binPath)
        {
            _owner = owner; _uri = uri; _binPath = binPath;
        }

        public Stream Begin()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(_binPath)!); } catch { }
            _tmpPath = _binPath + ".part";
            return File.Create(_tmpPath);
        }

        public void Commit(string contentType, int? statusCode, IReadOnlyDictionary<string, string>? headers)
        {
            if (_tmpPath is null) return;
            try
            {
                if (File.Exists(_binPath)) File.Delete(_binPath);
                File.Move(_tmpPath, _binPath);
                _owner.WriteMeta(_binPath, _uri, contentType, statusCode, headers, DateTimeOffset.UtcNow);
                _owner.EnforceDiskBudget();
            }
            catch { }
        }

        public void Abort()
        {
            try { if (_tmpPath is not null && File.Exists(_tmpPath)) File.Delete(_tmpPath); } catch { }
        }
    }
}
