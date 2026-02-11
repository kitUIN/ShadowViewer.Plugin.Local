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
}