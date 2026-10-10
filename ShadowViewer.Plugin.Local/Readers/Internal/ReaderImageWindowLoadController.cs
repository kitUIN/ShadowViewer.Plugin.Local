using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>以节点身份而非可变页码跟踪请求；离开窗口或章节切换即取消。</summary>
internal sealed class ReaderImageLoadTicket : IDisposable
{
    private readonly CancellationTokenSource cts = new();
    public ReaderImageLoadTicket(RenderNode node, CanvasDevice? device)
    {
        Node = node;
        Device = device;
        CancellationToken = cts.Token;
    }
    public RenderNode Node { get; }
    public CanvasDevice? Device { get; }
    public CancellationToken CancellationToken { get; }
    public async Task CancelAndDisposeAsync()
    {
        // 立即标记取消，但让 I/O 回调在后台执行，不能阻塞画布更新线程。
        try { await cts.CancelAsync().ConfigureAwait(false); }
        catch (AggregateException ex) { System.Diagnostics.Trace.TraceError($"Image cancellation error: {ex}"); }
        finally { cts.Dispose(); }
    }
    public void Dispose() => cts.Dispose();
}

/// <summary>管理可见页及邻页的加载、取消、释放和失败重试。</summary>
internal sealed class ReaderImageWindowLoadController(Func<DateTimeOffset>? getTime = null, TimeSpan? settleDelay = null)
{
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> getTime = getTime ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<RenderNode, ReaderImageLoadTicket> loading = new();
    private readonly Dictionary<RenderNode, (int Failures, DateTimeOffset After)> retries = new();
    private HashSet<RenderNode> wanted = new();
    private readonly TimeSpan settleDelay = settleDelay ?? TimeSpan.Zero;
    private HashSet<RenderNode> visibleWindow = new();
    private DateTimeOffset loadAfter;

    public void Reset()
    {
        List<ReaderImageLoadTicket> canceled;
        lock (gate)
        {
            wanted.Clear();
            visibleWindow.Clear();
            loadAfter = default;
            canceled = new List<ReaderImageLoadTicket>(loading.Values);
            loading.Clear();
            retries.Clear();
        }
        // 先摘除整批请求，再异步取消；旧回调不能干扰当前窗口，也不会阻塞渲染。
        foreach (var ticket in canceled) _ = ticket.CancelAndDisposeAsync();
    }

    public bool IsWanted(ReaderImageLoadTicket ticket)
    {
        lock (gate) return IsCurrent(ticket);
    }

    public bool TryCommit(ReaderImageLoadTicket ticket, ImageLoadingContext context, CanvasBitmap bitmap)
    {
        lock (gate)
        {
            return IsCurrent(ticket) && ticket.Node.TrySetImage(context, bitmap);
        }
    }

    private bool IsCurrent(ReaderImageLoadTicket ticket) =>
        !ticket.CancellationToken.IsCancellationRequested && !ticket.Node.IsRetired && wanted.Contains(ticket.Node) &&
        loading.TryGetValue(ticket.Node, out var current) && ReferenceEquals(current, ticket);

    public void Complete(ReaderImageLoadTicket ticket, bool succeeded)
    {
        lock (gate)
        {
            if (!loading.TryGetValue(ticket.Node, out var current) || !ReferenceEquals(current, ticket)) return;
            loading.Remove(ticket.Node);
            if (succeeded) retries.Remove(ticket.Node);
            else if (!ticket.CancellationToken.IsCancellationRequested && wanted.Contains(ticket.Node))
            {
                int failures = retries.TryGetValue(ticket.Node, out var retry) ? Math.Min(6, retry.Failures + 1) : 1;
                retries[ticket.Node] = (failures, getTime().AddSeconds(Math.Min(10, 0.5 * Math.Pow(2, failures - 1))));
            }
            ticket.Dispose();
        }
    }

    public void UpdateWindow(IReadOnlyList<RenderNode> layoutNodes, IReadOnlyList<RenderNode> allNodes,
        Rect viewportRect, int preloadRange, CanvasDevice? device, Func<ReaderImageLoadTicket, bool> enqueueBitmapLoad)
    {
        var nextWindow = BuildWindow(layoutNodes, allNodes, viewportRect, preloadRange, out var visible);
        List<ReaderImageLoadTicket> canceled;
        var released = new List<CanvasBitmap>();
        lock (gate)
        {
            var now = getTime();
            if (!visibleWindow.SetEquals(visible))
            {
                visibleWindow = visible;
                loadAfter = now + settleDelay;
            }
            wanted = nextWindow;
            canceled = loading.Values.Where(ticket => !wanted.Contains(ticket.Node)).ToList();
            foreach (var ticket in canceled) loading.Remove(ticket.Node);
            foreach (var node in new List<RenderNode>(retries.Keys))
                if (!wanted.Contains(node)) retries.Remove(node);

            foreach (var node in allNodes)
            {
                if (!wanted.Contains(node))
                {
                    // 即使位图未创建，也释放初始化阶段残留的字节数组。
                    if (node.IsLoaded || node.Ctx.Bytes != null || node.Preloaded)
                    {
                        var bitmap = node.DetachImage();
                        if (bitmap != null) released.Add(bitmap);
                    }
                }
            }
            // 可见图片先于预加载邻页入队，避免网络预取占满消费者。
            foreach (var node in visible.Concat(allNodes.Where(n => wanted.Contains(n) && !visible.Contains(n))))
            {
                // 快速跳页只更新目标窗口，短暂稳定后再加载，避免中间页堆满队列。
                if (now < loadAfter) break;
                if (!wanted.Contains(node) || node.IsRetired || node.IsLoaded || device == null || loading.ContainsKey(node) || node.ImageStrategy == null) continue;
                if (retries.TryGetValue(node, out var retry) && getTime() < retry.After) continue;
                var ticket = new ReaderImageLoadTicket(node, device);
                loading.Add(node, ticket);
                if (enqueueBitmapLoad(ticket)) continue;
                loading.Remove(node);
                ticket.Dispose(); // 队满可以在下一帧重试，不算网络失败。
            }
        }
        foreach (var ticket in canceled) _ = ticket.CancelAndDisposeAsync();
        if (released.Count > 0)
        {
            // 大跳页会淘汰整个预加载窗口；原生资源释放不能挡住画布或新页入队。
            // 只持有已摘除的位图，不能在后台 Dispose 节点，否则会误释放重载后的图片。
            _ = Task.Run(() =>
            {
                foreach (var bitmap in released)
                {
                    try { bitmap.Dispose(); }
                    catch (Exception ex) { System.Diagnostics.Trace.TraceError($"Image release error: {ex}"); }
                }
            });
        }
    }

    private static HashSet<RenderNode> BuildWindow(IReadOnlyList<RenderNode> layoutNodes,
        IReadOnlyList<RenderNode> allNodes, Rect viewport, int preloadRange, out HashSet<RenderNode> visible)
    {
        visible = new HashSet<RenderNode>();
        foreach (var node in layoutNodes)
        {
            var bounds = node.Bounds;
            if (bounds.X < viewport.X + viewport.Width && bounds.X + bounds.Width > viewport.X &&
                bounds.Y < viewport.Y + viewport.Height && bounds.Y + bounds.Height > viewport.Y)
                visible.Add(node);
        }
        int first = int.MaxValue, last = -1;
        for (int i = 0; i < allNodes.Count; i++)
        {
            if (!visible.Contains(allNodes[i])) continue;
            first = Math.Min(first, i); last = i;
        }
        if (last < 0) return new HashSet<RenderNode>();
        int range = Math.Max(0, preloadRange);
        var result = new HashSet<RenderNode>();
        for (int i = Math.Max(0, first - range); i <= Math.Min(allNodes.Count - 1, (long)last + range); i++) result.Add(allNodes[i]);
        return result;
    }
}
