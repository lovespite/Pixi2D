namespace Pixi2D.Host.Assets;

/// <summary>底层资源 provider (file/http/...)。AssetLoader 按 scheme dispatch。</summary>
public interface IAssetProvider
{
    bool CanHandle(Uri uri);
    /// <summary>
    /// 加载资源；可选 progress 回调 (loadedBytes, totalBytes; total 未知时 -1)。
    /// </summary>
    ValueTask<AssetData> LoadAsync(Uri uri, IAssetWriteSink? sink, Action<long, long>? progress, CancellationToken ct);
}

/// <summary>
/// 写盘 sink：provider 用来流式落盘到磁盘缓存。AssetCache 提供具体实现；本地 FileProvider 一般传 null (已在盘上)。
/// </summary>
public interface IAssetWriteSink
{
    /// <summary>开始写入；返回临时文件流。</summary>
    Stream Begin();
    /// <summary>提交临时文件为正式缓存条目。</summary>
    void Commit(string contentType, int? statusCode, IReadOnlyDictionary<string, string>? headers);
    /// <summary>放弃 (异常时清理)。</summary>
    void Abort();
}
