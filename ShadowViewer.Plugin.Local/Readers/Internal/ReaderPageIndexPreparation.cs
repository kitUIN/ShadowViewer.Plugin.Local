using System;
using System.Collections.Generic;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>跟踪整章尺寸探测；队列暂满时保留未入队节点，全部探测结束后才允许发布分页。</summary>
internal sealed class ReaderPageIndexPreparation
{
    private readonly object gate = new();
    private readonly Queue<RenderNode> pending = new();
    private readonly HashSet<RenderNode> outstanding = new();
    private bool? buildIndex;

    public bool IsReady { get { lock (gate) return outstanding.Count == 0; } }

    public void Add(IEnumerable<RenderNode> nodes, bool buildPageIndex = true)
    {
        lock (gate)
        {
            // 一章内固定使用首批节点的设置，避免中途切换留下部分探测或改变等待条件。
            buildIndex ??= buildPageIndex;
            if (!buildIndex.Value) return;
            foreach (var node in nodes)
                if (!node.IsSizeLoaded && outstanding.Add(node)) pending.Enqueue(node);
        }
    }

    public void Dispatch(Func<RenderNode, bool> enqueue)
    {
        lock (gate)
        {
            while (pending.TryPeek(out var node) && enqueue(node)) pending.Dequeue();
        }
    }

    public void Complete(RenderNode node)
    {
        // 没有尺寸或读取失败也算一次完成，保留整图占位并允许之后按需重试。
        lock (gate) outstanding.Remove(node);
    }

    public void Reset()
    {
        lock (gate)
        {
            pending.Clear();
            outstanding.Clear();
            buildIndex = null;
        }
    }
}
