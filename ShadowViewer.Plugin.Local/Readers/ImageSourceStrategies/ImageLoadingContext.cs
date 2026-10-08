using System;
using System.Threading;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

/// <summary>
/// 表示图像加载时的上下文信息，包括资源标识、目标尺寸和原始字节数据。
/// </summary>
public class ImageLoadingContext
{
    /// <summary>
    /// 用于标识图像资源的对象，通常为文件路径、URI 或其他自定义标识符。
    /// </summary>
    public object Source { get; set; } = null!;

    /// <summary>
    /// 实际的图像尺寸（以像素为单位），用于指定加载或缩放目标。
    /// </summary>
    public Size Size { get; set; }

    /// <summary>
    /// 图像字节数据（若已预加载），否则为 <c>null</c>。
    /// </summary>
    public byte[]? Bytes { get; set; }

    /// <summary>
    /// 缓存文件路径（若已落盘缓存），否则为 <c>null</c>。
    /// </summary>
    public string? CachedFilePath { get; set; }

    /// <summary>当前请求的取消令牌；自定义策略可在原有 Hook 中读取。</summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>尺寸是否来自有效图像，零尺寸表示当前请求尚未获取尺寸。</summary>
    public bool HasValidSize => Size.Width > 0 && Size.Height > 0 &&
                                double.IsFinite(Size.Width) && double.IsFinite(Size.Height);
}
