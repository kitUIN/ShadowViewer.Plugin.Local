using Windows.Foundation;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using System.Threading;

namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 表示要在画布上绘制的节点，包含页码、在世界坐标系中的边界、位图资源、资源来源和加载上下文。
/// </summary>
public class RenderNode
{
    /// <summary>
    /// 原始页码。
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// 在世界坐标系中的矩形区域。
    /// </summary>
    public Rect Bounds;

    /// <summary>
    /// ImageStrategy
    /// </summary>
    public IImageSourceStrategy? ImageStrategy { get; set; }

    /// <summary>
    /// Win2D 位图资源，绘制完成或加载完成后将被设置。
    /// </summary>
    public CanvasBitmap? Bitmap
    {
        get { lock (bitmapLock) return bitmap; }
        set => SetBitmap(value);
    }

    /// <summary>
    /// 资源路径或 URL，也可以是任意用于标识资源的对象。
    /// </summary>
    public object Source { get; init; } = null!;

    /// <summary>
    /// 图片加载上下文，包含加载策略和相关状态。
    /// </summary>
    public ImageLoadingContext Ctx { get; init; } = null!;

    private readonly object bitmapLock = new();
    private CanvasBitmap? bitmap;
    private volatile bool isRetired;
    internal SemaphoreSlim ImageLoadGate { get; } = new(1, 1);
    internal bool IsSourceInitialized { get; private set; }
    internal bool IsRetired => isRetired;

    /// <summary>
    /// 如果 <see cref="Bitmap"/> 不为 <c>null</c> 则表示已加载。
    /// </summary>
    public bool IsLoaded { get { lock (bitmapLock) return bitmap != null; } }

    /// <summary>
    /// 是否已加载实际尺寸（不是默认占位尺寸）。
    /// </summary>
    public bool IsSizeLoaded { get; set; }

    /// <summary>
    /// 是否预加载
    /// </summary>
    public bool Preloaded { get; set; }

    /// <summary>
    /// 提供对位图的安全访问并锁定，以便在绘制时不会被释放。
    /// </summary>
    public void UseBitmap(System.Action<CanvasBitmap> action)
    {
        lock (bitmapLock)
        {
            if (bitmap != null)
            {
                action(bitmap);
            }
        }
    }

    /// <summary>
    /// 设置位图。
    /// </summary>
    public void SetBitmap(CanvasBitmap? bitmap)
    {
        lock (bitmapLock)
        {
            if (ReferenceEquals(this.bitmap, bitmap)) return;
            if (isRetired) { bitmap?.Dispose(); return; }
            this.bitmap?.Dispose();
            this.bitmap = bitmap;
        }
    }

    internal ImageLoadingContext CreateLoadContext(CancellationToken token)
    {
        lock (bitmapLock)
        {
            return new ImageLoadingContext
            {
                Source = Ctx.Source,
                Size = IsSizeLoaded ? Ctx.Size : default,
                Bytes = Ctx.Bytes,
                CachedFilePath = Ctx.CachedFilePath,
                CancellationToken = token
            };
        }
    }

    internal bool TryApplyImageContext(ImageLoadingContext context)
    {
        lock (bitmapLock)
        {
            if (isRetired || context.CancellationToken.IsCancellationRequested) return false;
            ApplyContext(context);
            return true;
        }
    }

    internal bool TrySetImage(ImageLoadingContext context, CanvasBitmap image)
    {
        lock (bitmapLock)
        {
            if (isRetired || context.CancellationToken.IsCancellationRequested) return false;
            ApplyContext(context);
            bitmap?.Dispose();
            bitmap = image;
            Preloaded = true;
            return true;
        }
    }

    private void ApplyContext(ImageLoadingContext context)
    {
        Ctx.Bytes = context.Bytes;
        Ctx.CachedFilePath = context.CachedFilePath;
        if (context.HasValidSize)
        {
            Ctx.Size = context.Size;
            IsSizeLoaded = true;
        }
        IsSourceInitialized = true;
    }

    /// <summary>永久移除节点，拒绝尚未完成的后台加载回写。</summary>
    public void Retire()
    {
        lock (bitmapLock)
        {
            isRetired = true;
            Dispose();
        }
    }

    /// <summary>
    /// 释放托管的位图资源并将其引用置空。
    /// </summary>
    public void Dispose()
    {
        lock (bitmapLock)
        {
            bitmap?.Dispose();
            bitmap = null;
            // 自定义策略的字节 Hook 也应随位图释放；重入窗口后重新执行初始化和预加载。
            if (Ctx.Bytes != null) IsSourceInitialized = false;
            Ctx.Bytes = null;
            Preloaded = false;
        }
    }
}
