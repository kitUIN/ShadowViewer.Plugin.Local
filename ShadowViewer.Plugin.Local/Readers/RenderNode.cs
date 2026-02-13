using Windows.Foundation;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 表示要在画布上绘制的节点，包含页码、在世界坐标系中的边界、位图资源、资源来源和加载上下文。
/// </summary>
public class RenderNode
{
    /// <summary>
    /// 原始页码。
    /// </summary>
    public int PageIndex;

    /// <summary>
    /// 在世界坐标系中的矩形区域。
    /// </summary>
    public Rect Bounds;

    /// <summary>
    /// Win2D 位图资源，绘制完成或加载完成后将被设置。
    /// </summary>
    public CanvasBitmap? Bitmap;

    /// <summary>
    /// 资源路径或 URL，也可以是任意用于标识资源的对象。
    /// </summary>
    public object Source = null!;

    /// <summary>
    /// 图片加载上下文，包含加载策略和相关状态。
    /// </summary>
    public ImageLoadingContext Ctx = null!;

    /// <summary>
    /// 如果 <see cref="Bitmap"/> 不为 <c>null</c> 则表示已加载。
    /// </summary>
    public bool IsLoaded => Bitmap != null;

    /// <summary>
    /// 是否已加载实际尺寸（不是默认占位尺寸）。
    /// </summary>
    public bool IsSizeLoaded;

    /// <summary>
    /// 释放托管的位图资源并将其引用置空。
    /// </summary>
    public void Dispose()
    {
        Bitmap?.Dispose();
        Bitmap = null;
    }
}