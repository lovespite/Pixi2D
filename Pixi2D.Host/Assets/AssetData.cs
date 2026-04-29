namespace Pixi2D.Host.Assets;

/// <summary>资源加载结果。 <see cref="Bytes"/> 在大文件 (≥ MemoryThresholdBytes) 时按需读自 <see cref="DiskPath"/>。</summary>
public sealed record AssetData(
    Uri Source,
    byte[] Bytes,
    string? ContentType,
    DateTimeOffset FetchedAt,
    bool FromCache,
    string? DiskPath = null,
    int? StatusCode = null,
    IReadOnlyDictionary<string, string>? Headers = null)
{
    public long SizeBytes => Bytes.LongLength;
}

/// <summary>下载进度事件 (≥1MB 文件才发)。</summary>
public sealed record AssetProgress(int RequestId, Uri Source, long Loaded, long Total);
