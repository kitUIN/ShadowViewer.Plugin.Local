using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

/// <summary>
/// 基于本地文件的图像加载策略。该策略可处理本地路径或 <see cref="StorageFile"/> 并
/// 从文件中读取图像属性和字节数据以填充 <see cref="ImageLoadingContext"/>。
/// </summary>
public class LocalFileStrategy : IImageSourceStrategy
{
    /// <summary>
    /// 判断给定的资源标识是否为本地文件或可通过本地路径访问的资源。
    /// 支持 <see cref="StorageFile"/> 实例、绝对路径以及以 "ms-appx:" 或 "ms-appdata:" 为前缀的路径。
    /// </summary>
    /// <param name="source">要检查的资源标识（可以是 <see cref="StorageFile"/> 或字符串路径）。</param>
    /// <returns>如果可以处理该资源则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public bool CanHandle(object source)
    {
        if (source is StorageFile) return true;
        if (source is string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return Path.IsPathRooted(path) || path.StartsWith("ms-appx:") || path.StartsWith("ms-appdata:");
        }

        return false;
    }


    /// <summary>
    /// 使用提供的 <see cref="ImageLoadingContext"/> 从本地文件加载图像信息。
    /// 该方法会尝试将 <see cref="ImageLoadingContext.Size"/> 设置为图像的尺寸，并将
    /// <see cref="ImageLoadingContext.Bytes"/> 填充为文件的字节数据（若可读）。
    /// </summary>
    /// <param name="ctx">包含资源标识和用于接收加载结果的上下文。</param>
    /// <returns>表示异步初始化操作的任务。</returns>
    public async Task InitImageAsync(ImageLoadingContext ctx)
    {
        var file = ctx.Source as StorageFile;
        if (file == null && ctx.Source is string path)
        {
            file = await StorageFile.GetFileFromPathAsync(path);
        }

        if (file != null)
        {
            var props = await file.Properties.GetImagePropertiesAsync();
            ctx.Size = new Size(props.Width, props.Height);
            using var stream = await file.OpenReadAsync();
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);
            ctx.Bytes = bytes;
        }
    }
}
