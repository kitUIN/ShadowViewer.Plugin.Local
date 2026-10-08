using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

/// <summary>
/// 基于本地文件的图像加载策略。该策略可处理本地路径或 <see cref="StorageFile"/> 并
/// 读取图像属性并记录文件路径；只有无物理路径的文件才回退到字节数据。
/// </summary>
public class LocalFileStrategy : IImageSourceStrategy
{
    /// <summary>
    /// 判断给定的资源标识是否为本地文件或可通过本地路径访问的资源。
    /// 支持 <see cref="StorageFile"/> 实例、绝对路径以及以 "ms-appx:" 或 "ms-appdata:" 为前缀的路径。
    /// </summary>
    /// <param name="source">要检查的资源标识（可以是 <see cref="StorageFile"/> 或字符串路径）。</param>
    /// <returns>如果可以处理该资源则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public virtual bool CanHandle(object source)
    {
        if (source is IUiPicture picture)
        {
            source = picture.SourcePath;
        }

        if (source is StorageFile) return true;
        if (source is string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Path.IsPathRooted(path) || IsApplicationUri(path);
        }

        return false;
    }

    /// <summary>
    /// 解析普通路径或应用资源 URI。
    /// </summary>
    /// <param name="source">文件、路径或应用资源 URI。</param>
    /// <returns>解析得到的文件。</returns>
    protected Task<StorageFile?> GetStorageFile(object source) => GetStorageFile(source, CancellationToken.None);

    /// <summary>解析文件来源，并支持取消异步读取。</summary>
    /// <param name="source">文件、路径或应用资源 URI。</param>
    /// <param name="cancellationToken">当前图片加载请求的取消令牌。</param>
    /// <returns>解析得到的文件。</returns>
    protected async Task<StorageFile?> GetStorageFile(object source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source is StorageFile file) return file;
        string? path = source is IUiPicture picture ? picture.SourcePath : source as string;
        if (string.IsNullOrWhiteSpace(path)) return null;
        return IsApplicationUri(path)
            ? await StorageFile.GetFileFromApplicationUriAsync(new Uri(path)).AsTask(cancellationToken)
            : await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
    }

    private static bool IsApplicationUri(string path) =>
        path.StartsWith("ms-appx:", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("ms-appdata:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 使用提供的 <see cref="ImageLoadingContext"/> 从本地文件加载图像信息。
    /// 该方法会尝试将 <see cref="ImageLoadingContext.Size"/> 设置为图像的尺寸，并将
    /// <see cref="ImageLoadingContext.CachedFilePath"/> 设置为可直接打开的文件路径。
    /// </summary>
    /// <param name="ctx">包含资源标识和用于接收加载结果的上下文。</param>
    /// <returns>表示异步初始化操作的任务。</returns>
    public virtual async Task InitImageAsync(ImageLoadingContext ctx)
    {
        var file = await GetStorageFile(ctx.Source, ctx.CancellationToken);
        if (file == null) return;
        var props = await file.Properties.GetImagePropertiesAsync().AsTask(ctx.CancellationToken);
        ctx.Size = new Size(props.Width, props.Height);
        ctx.CachedFilePath = string.IsNullOrWhiteSpace(file.Path) ? null : file.Path;
    }

    /// <inheritdoc />
    public virtual async Task PreloadImageAsync(ImageLoadingContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(ctx.CachedFilePath)) return;
        var file = await GetStorageFile(ctx.Source, ctx.CancellationToken);
        if (file == null) return;
        using var stream = await file.OpenReadAsync().AsTask(ctx.CancellationToken);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        var bytes = new byte[checked((int)stream.Size)];
        await reader.LoadAsync((uint)bytes.Length).AsTask(ctx.CancellationToken);
        reader.ReadBytes(bytes);
        ctx.Bytes = bytes;
    }
}
