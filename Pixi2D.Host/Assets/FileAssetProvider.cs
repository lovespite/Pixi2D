namespace Pixi2D.Host.Assets;

/// <summary>file:// 与本地相对路径加载 (相对路径基于 <see cref="BaseDir"/>)。</summary>
public sealed class FileAssetProvider : IAssetProvider
{
    public string BaseDir { get; }

    public FileAssetProvider(string baseDir)
    {
        BaseDir = baseDir;
    }

    public bool CanHandle(Uri uri) => uri.IsFile;

    public ValueTask<AssetData> LoadAsync(Uri uri, IAssetWriteSink? sink, Action<long, long>? progress, CancellationToken ct)
    {
        var path = uri.LocalPath;
        if (!File.Exists(path))
            throw new FileNotFoundException("Asset not found: " + path, path);

        // 本地不入 sink (没必要复制)；直接读字节
        var bytes = File.ReadAllBytes(path);
        progress?.Invoke(bytes.LongLength, bytes.LongLength);
        var data = new AssetData(
            Source: uri,
            Bytes: bytes,
            ContentType: GuessContentType(path),
            FetchedAt: DateTimeOffset.UtcNow,
            FromCache: false,
            DiskPath: path);
        return new ValueTask<AssetData>(data);
    }

    private static string? GuessContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => "application/json",
            ".txt" or ".log" => "text/plain",
            ".xml" or ".pxml" => "application/xml",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => null,
        };
}
