namespace Pixi2D.Host.Assets;

/// <summary>分级缓存策略。</summary>
public sealed class AssetCachePolicy
{
    /// <summary>L1 内存阈值：仅 &lt; 此值的资源进 L1 (字节缓存)。默认 1MB。</summary>
    public long MemoryThresholdBytes { get; init; } = 1L * 1024 * 1024;
    /// <summary>L1 内存最大条目数。默认 256。</summary>
    public int MemoryMaxEntries { get; init; } = 256;
    /// <summary>L1 内存累计字节上限 (LRU 淘汰)。默认 32MB。</summary>
    public long MemoryMaxBytes { get; init; } = 32L * 1024 * 1024;

    /// <summary>L2 磁盘缓存目录。默认 <c>%TEMP%\Pixi2D\AssetCache</c>。</summary>
    public string DiskCacheDir { get; init; } = Path.Combine(Path.GetTempPath(), "Pixi2D", "AssetCache");
    /// <summary>L2 磁盘累计字节上限 (LRU 淘汰)。默认 512MB。</summary>
    public long DiskMaxBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>HTTP 客户端默认超时。</summary>
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>HTTP UA 头 (可由调用方覆盖)。</summary>
    public string HttpUserAgent { get; init; } = "Pixi2D-Host/0.7";

    public static AssetCachePolicy Default { get; } = new AssetCachePolicy();
}
