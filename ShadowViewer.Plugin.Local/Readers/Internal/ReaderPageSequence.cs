using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>从原图生成阅读页；两个半页共享原图资源，只保存各自的裁切区域。</summary>
internal static class ReaderPageSequence
{
    public static int Update(IReadOnlyList<RenderNode> sources, List<RenderNode> pages,
        ReadingMode mode, int currentIndex, bool splitWidePages = true,
        WidePageSplitScope scope = WidePageSplitScope.AllImages)
    {
        var current = currentIndex >= 0 && currentIndex < pages.Count ? pages[currentIndex] : null;
        var existing = pages.Where(n => n.ImageNode != n).ToDictionary(n => (n.ImageNode, n.SourceRegion.X));
        bool split = splitWidePages && (mode is ReadingMode.SpreadLtr or ReadingMode.SpreadRtl);
        bool IsEligible(RenderNode node) => scope == WidePageSplitScope.AllImages ||
            node.ImageStrategy?.IsLocalSource == true;
        // 比较等高宽度，避免仅因分辨率较高而拆开普通竖页。
        var ratios = sources.Where(n => IsEligible(n) && n.IsSizeLoaded && n.Ctx.Size.Width > 0 &&
                n.Ctx.Size.Height > 0 && n.Ctx.Size.Width < n.Ctx.Size.Height)
            .Take(100).Select(n => n.Ctx.Size.Width / n.Ctx.Size.Height).OrderBy(r => r).ToArray();
        double threshold = ratios.Length > 0 ? Math.Max(1.0, ratios[ratios.Length / 2] * 1.6) : 1.2;
        var next = new List<RenderNode>(sources.Count);
        foreach (var source in sources)
        {
            bool wide = split && IsEligible(source) && source.IsSizeLoaded && source.Ctx.Size.Height > 0 &&
                source.Ctx.Size.Width / source.Ctx.Size.Height >= threshold;
            if (!wide)
            {
                next.Add(source);
                continue;
            }
            foreach (double x in mode == ReadingMode.SpreadRtl ? new[] { 0.5, 0.0 } : new[] { 0.0, 0.5 })
            {
                if (!existing.TryGetValue((source, x), out var page))
                {
                    page = new RenderNode
                    {
                        OriginalNode = source,
                        SourceRegion = new Rect(x, 0, 0.5, 1),
                        Source = source.Source,
                        Ctx = source.Ctx
                    };
                }
                next.Add(page);
            }
        }

        // 尺寸晚到或切换模式时保持正在阅读的原图（以及已拆分的那一半）。
        int target = currentIndex;
        if (current != null)
        {
            target = next.IndexOf(current);
            if (target < 0) target = next.FindIndex(n => n.ImageNode == current.ImageNode);
        }
        pages.Clear();
        pages.AddRange(next);
        for (int i = 0; i < pages.Count; i++) pages[i].PageIndex = i;
        return pages.Count == 0 ? 0 : Math.Clamp(target, 0, pages.Count - 1);
    }
}
