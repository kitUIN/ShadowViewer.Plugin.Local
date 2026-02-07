using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
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
        if (ctx.Source is string url)
        {
            var file = await DownloadToCacheAsync(url);
            if (file != null)
            {
                using var stream = await file.OpenReadAsync();
                using var reader = new DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)stream.Size);
                var bytes = new byte[stream.Size];
                reader.ReadBytes(bytes);
                ctx.Bytes = bytes;
                var props = await file.Properties.GetImagePropertiesAsync();
                ctx.Size = new Size(props.Width, props.Height);
            }
        }
    }

    /// <summary>
    /// 将远程 URL 下载到应用的临时文件夹并返回对应的 <see cref="StorageFile"/>。
    /// 已存在的缓存文件将被直接返回以避免重复下载。
    /// </summary>
    /// <param name="url">要下载的远程资源 URL。</param>
    /// <returns>下载完成后返回对应的缓存 <see cref="StorageFile"/>，若失败则返回 <c>null</c>。</returns>
    private async Task<StorageFile?> DownloadToCacheAsync(string url)
    {
        try
        {
            var cacheFolder = ApplicationData.Current.TemporaryFolder;
            var fileName = GetCacheFileName(url);

            // 2. Check if file exists
            var item = await cacheFolder.TryGetItemAsync(fileName);
            if (item is StorageFile existingFile)
            {
                return existingFile;
            }

            // 3. Download
            var buffer = await Client.GetByteArrayAsync(url);
            var file = await cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(file, buffer);
            return file;
        }
        catch
        {
            return null;
        }
        finally
        {
            // Remove from pending list after completion (success or fail) so future retries are possible
        }
    }

    /// <summary>
    /// 根据 URL 计算用于命名缓存文件的散列文件名。
    /// </summary>
    /// <param name="url">输入的 URL。</param>
    /// <returns>基于 URL 的散列字符串并附加默认扩展名。</returns>
    private string GetCacheFileName(string url)
    {
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString() + ".img"; // Append extension or detect content type
        }
    }
}