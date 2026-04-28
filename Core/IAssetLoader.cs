using SharpDX.Direct2D1;

namespace Pixi2D.Core;

/// <summary>
/// 资源加载器抽象。<br />
/// 用于在 XML(DSL) 反序列化或控件无参构造需要外部资源 (位图、Sprite 等) 时，
/// 由应用层统一注入资源解析逻辑。通常通过 <see cref="UIContext.Assets"/> 访问。
/// </summary>
public interface IAssetLoader
{
    /// <summary>
    /// 按 key 加载位图。加载失败返回 null（调用方需处理 fallback）。
    /// </summary>
    Bitmap1? LoadBitmap(string key);

    /// <summary>
    /// 按 key 加载 Sprite。默认实现基于 <see cref="LoadBitmap(string)"/>。
    /// </summary>
    Sprite? LoadSprite(string key)
    {
        var bmp = LoadBitmap(key);
        return bmp is null ? null : new Sprite(bmp);
    }
}

/// <summary>
/// 永远返回 null 的 <see cref="IAssetLoader"/> 默认实现。
/// 在未注入具体加载器时作为兜底。
/// </summary>
public sealed class NullAssetLoader : IAssetLoader
{
    public static readonly NullAssetLoader Instance = new();
    private NullAssetLoader() { }

    public Bitmap1? LoadBitmap(string key) => null;
}
