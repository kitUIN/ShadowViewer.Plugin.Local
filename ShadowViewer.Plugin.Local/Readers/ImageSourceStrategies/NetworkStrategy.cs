using Microsoft.UI.Xaml.Media.Imaging;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

/// <summary>
/// 基于网络的图像加载策略。该策略会将远程 URL 下载到临时缓存文件并
/// 从缓存文件读取图像字节与尺寸信息，用于填充 <see cref="ImageLoadingContext"/>。
/// </summary>
public class NetworkStrategy : IImageSourceStrategy
{
    /// <summary>
    /// 用于执行 HTTP 请求的共享 <see cref="HttpClient"/> 实例。
    /// </summary>
    private static readonly HttpClient Client = new();

    /// <summary>
    /// 判断给定的资源标识是否为可通过 HTTP/HTTPS 访问的 URL。
    /// </summary>
    /// <param name="source">要检查的资源标识，通常为字符串 URL。</param>
    /// <returns>如果值为 HTTP 或 HTTPS URL 则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public bool CanHandle(object source)
    {
        if (source is IUiPicture picture)
        {
            source = picture.SourcePath;
        }
        if (source is string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// 使用提供的 <see cref="ImageLoadingContext"/> 从远程 URL 初始化图像数据。
    /// 若成功会将 <see cref="ImageLoadingContext.Bytes"/> 填充为图像字节，并设置
    /// <see cref="ImageLoadingContext.Size"/> 为图像尺寸。
    /// </summary>
    /// <param name="ctx">包含 URL 的加载上下文。</param>
    /// <returns>表示异步初始化操作的任务。</returns>
    public async Task InitImageAsync(ImageLoadingContext ctx)
    {
        var source = ctx.Source;
        if (source is IUiPicture picture)
        {
            source = picture.SourcePath;
        }
        if (source is string url)
        {
            var bytes = await Client.GetByteArrayAsync(url);
            ctx.Bytes = bytes;
            using var stream = new MemoryStream(bytes);
            using var randomAccessStream = stream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            ctx.Size = new Size((int)decoder.PixelWidth, (int)decoder.PixelHeight);
        }
    }
}