using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>串行访问单个节点的策略，使用独立上下文，只有当前窗口的请求能发布位图。</summary>
internal sealed class ReaderImageLoadService(
    ReaderImageWindowLoadController window,
    Func<ImageLoadingContext, CanvasDevice, CancellationToken, Task<CanvasBitmap?>> decodeAsync,
    Action<RenderNode> sizeChanged,
    Action<Exception> reportError,
    Func<RenderNode, ImageLoadingContext, Exception, Task>? recoverDecodeAsync = null)
{
    public async Task InitializeAsync(RenderNode node, CancellationToken cancellationToken)
    {
        await node.ImageLoadGate.WaitAsync(cancellationToken);
        try
        {
            if (node.IsRetired || node.IsSourceInitialized || node.ImageStrategy == null) return;
            var context = node.CreateLoadContext(cancellationToken);
            await node.ImageStrategy.InitImageAsync(context).WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            node.TryApplyImageContext(context);
        }
        finally { node.ImageLoadGate.Release(); }
    }

    public async Task LoadAsync(ReaderImageLoadTicket ticket, CancellationToken pipelineToken)
    {
        bool succeeded = false;
        CanvasBitmap? bitmap = null;
        bool entered = false;
        var node = ticket.Node;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ticket.CancellationToken, pipelineToken);
        var token = linked.Token;
        try
        {
            if (!window.IsWanted(ticket) || ticket.Device == null || node.ImageStrategy == null) return;
            await node.ImageLoadGate.WaitAsync(token);
            entered = true;
            token.ThrowIfCancellationRequested();
            var context = node.CreateLoadContext(token);
            if (!node.IsSourceInitialized) await node.ImageStrategy.InitImageAsync(context).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (!node.Preloaded) await node.ImageStrategy.PreloadImageAsync(context).WaitAsync(token);
            token.ThrowIfCancellationRequested();

            try
            {
                var decoding = decodeAsync(context, ticket.Device, token);
                try { bitmap = await decoding.WaitAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 原生解码可能无法立即取消；释放消费者，并单独回收其迟到结果。
                    _ = DisposeAbandonedBitmapAsync(decoding);
                    throw;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (recoverDecodeAsync != null) await recoverDecodeAsync(node, context, ex);
                throw;
            }
            token.ThrowIfCancellationRequested();
            if (bitmap == null) throw new InvalidOperationException("Image decoding did not produce a bitmap.");
            if (!context.HasValidSize) context.Size = bitmap.Size;
            bool changed = !node.IsSizeLoaded || !node.Ctx.Size.Equals(context.Size);
            succeeded = window.TryCommit(ticket, context, bitmap);
            if (!succeeded) return;
            bitmap = null; // 已转移给节点，后续由窗口淘汰或章节清理释放。
            if (changed) sizeChanged(node);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { reportError(ex); }
        finally
        {
            bitmap?.Dispose();
            if (entered) node.ImageLoadGate.Release();
            window.Complete(ticket, succeeded);
        }
    }

    private static async Task DisposeAbandonedBitmapAsync(Task<CanvasBitmap?> decoding)
    {
        try { (await decoding.ConfigureAwait(false))?.Dispose(); }
        catch (Exception) { } // 旧请求已取消；观察解码异常，避免未观察任务或位图泄漏。
    }
}
