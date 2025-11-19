using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
using System.Collections.Concurrent;
using System.Diagnostics;
using PixelFormat = SharpDX.WIC.PixelFormat;

namespace Pixi2D;

/// <summary>
/// 纹理管理器类。
/// 负责加载、缓存和管理 Bitmap1 资源。
/// </summary>
public static class Textures
{
    private static readonly ConcurrentDictionary<string, Bitmap1> _cache = [];
    private static DeviceContext? _context;
    private static ImagingFactory? _imagingFactory;
    private static Bitmap1? _emptyBitmap;
    private static Bitmap1? _eBitmap;

    public static bool Initialized => _context is not null && _imagingFactory is not null;

    /// <summary>
    /// 获取一个空的 1x1 透明纹理。
    /// </summary>
    public static Bitmap1 Empty => _emptyBitmap ?? throw new InvalidOperationException("Texture manager not initialized.");
    public static Bitmap1 E => _eBitmap ?? throw new InvalidOperationException("Texture manager not initialized.");

    /// <summary>
    /// 初始化纹理管理器。必须在加载任何纹理前调用。
    /// </summary>
    /// <param name="renderTarget">当前的渲染目标 (将被转换为 DeviceContext)。</param>
    public static bool Initialize(RenderTarget renderTarget)
    {
        // 尝试获取 DeviceContext (Direct2D 1.1)
        try
        {
            _context = renderTarget.QueryInterface<DeviceContext>();
        }
        catch
        {
            return false; // 不支持 Direct2D 1.1
        }

        _imagingFactory ??= new ImagingFactory();

        // 创建默认的“空”纹理 (1x1 透明)
        _emptyBitmap ??= Create1x1Bitmap(new RawColor4(0, 0, 0, 0));
        // 创建默认的“错误”纹理 (半透明红色填充)
        _eBitmap ??= CreateETexture();

        return true;
    }

    /// <summary>
    /// 手动缓存纹理 (从文件路径)。
    /// </summary>
    /// <param name="name">纹理名称 (用于 Get 获取)。</param>
    /// <param name="imageFilePath">图片文件路径。</param>
    public static void Add(string name, string imageFilePath)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
        {
            AddToCache(name, E);
            return;
        }

        try
        {
            using var decoder = new BitmapDecoder(_imagingFactory, imageFilePath, DecodeOptions.CacheOnDemand);
            var bitmap = LoadFromDecoder(decoder);
            AddToCache(name, bitmap);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WRN] Failed to load texture from '{imageFilePath}': {ex.Message}");
            AddToCache(name, E);
        }
    }

    public static void Add(string name, ReadOnlySpan<byte> imageData)
    {
        EnsureInitialized();
        using var stream = new MemoryStream();
        stream.Write(imageData);
        stream.Seek(0, SeekOrigin.Begin);
        Add(name, stream);
    }

    public static void Add(string name, Bitmap1 bitmap)
    {
        EnsureInitialized();
        AddToCache(name, bitmap);
    }

    public static async Task LoadFromUrl(string name, string url)
    {
        EnsureInitialized();
        try
        { 
            using var data = await SharedClient.GetStreamAsync(url);
            Add(name, data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WRN] Failed to load texture from URL '{url}': {ex.Message}");
            AddToCache(name, E);
        }
    }

    private static HttpClient? _sharedClient;
    public static void UseHttpClient(HttpClient client)
    {
        _sharedClient = client;
    }
    public static HttpClient SharedClient => _sharedClient ??= new HttpClient();

    /// <summary>
    /// 手动缓存纹理 (从流)。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <param name="stream">图片数据流。</param>
    public static void Add(string name, Stream? stream)
    {
        EnsureInitialized();
        if (stream is null || stream.Length == 0 || !stream.CanRead)
        {
            AddToCache(name, Empty);
            return;
        }

        try
        {
            using var decoder = new BitmapDecoder(_imagingFactory, stream, DecodeOptions.CacheOnDemand);
            var bitmap = LoadFromDecoder(decoder);
            AddToCache(name, bitmap);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WRN] Failed to load texture from stream: {ex.Message}");
            AddToCache(name, E);
        }

    }

    /// <summary>
    /// 手动缓存纹理 (从内存数据)。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <param name="data">图片字节数据。</param>
    public static void Add(string name, ReadOnlyMemory<byte> data)
    {
        EnsureInitialized();
        using var stream = new MemoryStream(data.ToArray());
        Add(name, stream);
    }

    ///// <summary>
    ///// 手动缓存纹理 (从 System.Drawing.Bitmap)。
    ///// </summary>
    ///// <param name="name">纹理名称。</param>
    ///// <param name="sysBitmap">GDI+ Bitmap 对象。</param>
    //public static void Add(string name, System.Drawing.Bitmap sysBitmap)
    //{
    //    EnsureInitialized();

    //    // 锁定 System.Drawing.Bitmap 的位
    //    var rect = new System.Drawing.Rectangle(0, 0, sysBitmap.Width, sysBitmap.Height);
    //    var bmpData = sysBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

    //    try
    //    {
    //        // 创建 DataStream
    //        var dataStream = new SharpDX.DataStream(bmpData.Scan0, sysBitmap.Height * bmpData.Stride, true, false);

    //        // 准备 Bitmap 属性
    //        var size = new Size2(sysBitmap.Width, sysBitmap.Height);
    //        var props = new BitmapProperties1
    //        {
    //            PixelFormat = new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
    //            BitmapOptions = BitmapOptions.None
    //        };

    //        // 创建 Bitmap1
    //        var bitmap = new Bitmap1(_context, size, dataStream, bmpData.Stride, props);
    //        AddToCache(name, bitmap);
    //    }
    //    finally
    //    {
    //        sysBitmap.UnlockBits(bmpData);
    //    }
    //}

    /// <summary>
    /// 获取纹理。不存在时返回一个空的纹理(Empty)。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <returns>对应的 Bitmap1，如果未找到则返回 Empty 纹理。</returns>
    public static Bitmap1 Get(string name)
    {
        if (_cache.TryGetValue(name, out var bitmap))
        {
            return bitmap;
        }

        return Empty;
    }

    /// <summary>
    /// 移除并销毁指定名称的纹理。
    /// </summary>
    public static void Remove(string name)
    {
        if (_cache.TryRemove(name, out var bitmap))
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// 清除所有缓存的纹理并释放资源。
    /// (不会清除 Empty 纹理)
    /// </summary>
    public static void Clear()
    {
        foreach (var bmp in _cache.Values)
        {
            bmp.Dispose();
        }
        _cache.Clear();
    }

    // --- Helper Methods ---

    private static void EnsureInitialized()
    {
        if (_context == null || _imagingFactory == null)
            throw new InvalidOperationException("Texture manager must be initialized with a RenderTarget first.");
    }

    private static void AddToCache(string name, Bitmap1 bitmap)
    {
        // 如果已存在同名纹理，先释放旧的
        if (_cache.TryGetValue(name, out var old))
        {
            old.Dispose();
        }
        _cache[name] = bitmap;
    }

    private static Bitmap1 LoadFromDecoder(BitmapDecoder decoder)
    {
        using var frame = decoder.GetFrame(0);
        using var converter = new FormatConverter(_imagingFactory);

        // 转换格式为 D2D 兼容的 PBGRA
        converter.Initialize(frame, PixelFormat.Format32bppPBGRA);

        return Bitmap1.FromWicBitmap(_context, converter);
    }

    private static Bitmap1 Create1x1Bitmap(RawColor4 color)
    {
        // 准备 1x1 像素数据 (B, G, R, A)
        byte b = (byte)(color.B * 255);
        byte g = (byte)(color.G * 255);
        byte r = (byte)(color.R * 255);
        byte a = (byte)(color.A * 255);

        var pixelData = new byte[] { b, g, r, a };

        // 创建 DataStream
        using var dataStream = SharpDX.DataStream.Create(pixelData, true, false);

        var props = new BitmapProperties1(
            new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
            96, 96, BitmapOptions.None);

        return new Bitmap1(_context, new Size2(1, 1), dataStream, 4, props);
    }

    /// <summary>
    /// 创建一个错误纹理 (半透明红色填充4x4)。
    /// </summary>
    /// <returns></returns>
    private static Bitmap1 CreateETexture()
    {
        var pixelData = new byte[]
        {
            0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128,
            0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128,
            0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128,
            0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128, 0, 0, 255, 128,
        };
        using var dataStream = SharpDX.DataStream.Create(pixelData, true, false);
        var props = new BitmapProperties1(
            new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
            96, 96, BitmapOptions.None);
        return new Bitmap1(_context!, new Size2(4, 4), dataStream, 16, props);
    }
}