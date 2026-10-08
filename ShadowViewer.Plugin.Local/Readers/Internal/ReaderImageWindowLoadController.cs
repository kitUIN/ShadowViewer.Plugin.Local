using System;
using System.Collections.Generic;
using System.Threading;
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
    public void Cancel() => cts.Cancel();
    public void Dispose() => cts.Dispose();
}

/// <summary>管理可见页及邻页的加载、取消、释放和失败重试。</summary>
internal sealed class ReaderImageWindowLoadController(Func<DateTimeOffset>? getTime = null)
{
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> getTime = getTime ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<RenderNode, ReaderImageLoadTicket> loading = new();
    private readonly Dictionary<RenderNode, (int Failures, DateTimeOffset After)> retries = new();
    private HashSet<RenderNode> wanted = new();

    public void Reset()
    {
        lock (gate)
        {
            wanted.Clear();
            foreach (var ticket in loading.Values) { ticket.Cancel(); ticket.Dispose(); }
            loading.Clear();
            retries.Clear();
        }
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
        lock (gate)
        {
            wanted = nextWindow;
            foreach (var node in new List<RenderNode>(loading.Keys))
            {
                if (wanted.Contains(node)) continue;
                var ticket = loading[node];
                loading.Remove(node);
                ticket.Cancel(); ticket.Dispose();
            }
            foreach (var node in new List<RenderNode>(retries.Keys))
                if (!wanted.Contains(node)) retries.Remove(node);

            foreach (var node in allNodes)
            {
                if (!wanted.Contains(node))
                {
                    // 即使位图未创建，也释放初始化阶段残留的字节数组。
                    if (node.IsLoaded || node.Ctx.Bytes != null || node.Preloaded) node.Dispose();
                }
            }
            // 可见图片先于预加载邻页入队，避免网络预取占满消费者。
            foreach (var node in visible.Concat(allNodes.Where(n => wanted.Contains(n) && !visible.Contains(n))))
            {
                if (!wanted.Contains(node) || node.IsRetired || node.IsLoaded || device == null || loading.ContainsKey(node) || node.ImageStrategy == null) continue;
                if (retries.TryGetValue(node, out var retry) && getTime() < retry.After) continue;
                var ticket = new ReaderImageLoadTicket(node, device);
                loading.Add(node, ticket);
                if (enqueueBitmapLoad(ticket)) continue;
                loading.Remove(node);
                ticket.Dispose(); // 队满可以在下一帧重试，不算网络失败。
            }
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
