using ShadowViewer.Plugin.Local.Models.Interfaces;
using ShadowViewer.Plugin.Local.Readers.Internal;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

/// <summary>网络图片按需下载到磁盘缓存，初始化阶段只查询有效缓存。</summary>
public class NetworkStrategy : IImageSourceStrategy
{
    private static readonly HttpClient Client = new();
    private static readonly ReaderImageDiskCache Cache = new(Client,
        () => Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "manga_net_images"), ReadSizeAsync);

    /// <inheritdoc />
    public virtual bool CanHandle(object source)
    {
        var url = GetSourceUrl(source);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <inheritdoc />
    public virtual async Task InitImageAsync(ImageLoadingContext ctx)
    {
        var url = GetSourceUrl(ctx.Source);
        if (url == null) return;
        var cached = await Cache.TryGetAsync(url, ctx.CancellationToken);
        if (cached.HasValue) Apply(ctx, cached.Value);
    }

    /// <inheritdoc />
    public virtual async Task PreloadImageAsync(ImageLoadingContext ctx)
    {
        var url = GetSourceUrl(ctx.Source);
        if (url == null) return;
        Apply(ctx, await Cache.GetAsync(url, ctx.CancellationToken));
    }

    private static void Apply(ImageLoadingContext ctx, ReaderImageDiskCache.CachedImage image)
    {
        ctx.CachedFilePath = image.Path;
        ctx.Size = image.Size;
    }

    private static string? GetSourceUrl(object source) =>
        source is IUiPicture picture ? picture.SourcePath : source as string;

    internal static bool IsCacheDecodeFailure(Exception error) => error is IOException ||
        ((uint)error.HResult & 0xFFFFFF00u) == 0x88982F00u;

    internal Task InvalidateImageCacheAsync(ImageLoadingContext ctx)
    {
        var url = GetSourceUrl(ctx.Source);
        return url == null ? Task.CompletedTask : Cache.InvalidateAsync(url, ctx.CancellationToken);
    }

    private static async Task<Size?> ReadSizeAsync(string path, CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
        using var stream = await file.OpenReadAsync().AsTask(cancellationToken);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        return new Size(decoder.PixelWidth, decoder.PixelHeight);
    }
}
