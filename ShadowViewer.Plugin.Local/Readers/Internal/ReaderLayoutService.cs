using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>
/// 阅读器布局缓存状态，保存缩放采样和视口变化的计算结果。
/// </summary>
internal sealed class ReaderLayoutCacheState
{
    /// <summary>
    /// 获取或设置缓存缩放值。
    /// </summary>
    public float CachedScale { get; set; } = 1.0f;

    /// <summary>
    /// 获取或设置采样得到的众数高度。
    /// </summary>
    public double ModeHeight { get; set; }

    /// <summary>
    /// 获取或设置采样得到的众数宽度。
    /// </summary>
    public double ModeWidth { get; set; }

    /// <summary>
    /// 获取或设置缓存对应的视口高度。
    /// </summary>
    public float CachedViewHeight { get; set; }

    /// <summary>
    /// 获取或设置缓存对应的视口宽度。
    /// </summary>
    public float CachedViewWidth { get; set; }

    /// <summary>
    /// 获取或设置缓存对应的节点数量。
    /// </summary>
    public int CachedNodeCount { get; set; }

    /// <summary>
    /// 在清空内容后重置缓存状态。
    /// </summary>
    /// <returns>无返回值。</returns>
    public void ResetAfterClearItems()
    {
        CachedScale = 1.0f;
        CachedViewHeight = 0;
        CachedNodeCount = 0;
    }
}

/// <summary>
/// 阅读器布局服务，负责按阅读模式计算节点布局和缩放缓存。
/// </summary>
internal sealed class ReaderLayoutService
{
    /// <summary>
    /// 根据当前阅读模式更新布局节点。
    /// </summary>
    /// <param name="state">渲染状态对象。</param>
    /// <param name="allNodes">全部页面节点集合。</param>
    /// <param name="currentPageIndex">当前页索引。</param>
    /// <param name="pageSpacing">页面间距。</param>
    /// <param name="isFitToModeSize">是否按众数尺寸统一拟合。</param>
    /// <param name="viewSize">当前视口大小。</param>
    /// <param name="cache">布局缓存状态。</param>
    /// <returns>无返回值。</returns>
    public void UpdateActiveLayout(
        EngineState state,
        List<RenderNode> allNodes,
        int currentPageIndex,
        float pageSpacing,
        bool isFitToModeSize,
        Vector2 viewSize,
        ReaderLayoutCacheState cache)
    {
        lock (state.LayoutNodes)
        {
            state.LayoutNodes.Clear();

            if (state.CurrentMode == ReadingMode.VerticalScroll)
            {
                float currentY = 0;
                float spacing = pageSpacing;
                float scale;

                lock (allNodes)
                {
                    scale = GetScale(allNodes, viewSize, cache);

                    foreach (var node in allNodes)
                    {
                        double scaledWidth;
                        double scaledHeight;
                        if (isFitToModeSize && cache.ModeWidth > 0 && cache.ModeHeight > 0)
                        {
                            // 先按众数尺寸做等比拟合，再叠加全局缩放，能兼顾统一观感与完整显示。
                            double fitScale = Math.Min(cache.ModeWidth / node.Ctx.Size.Width, cache.ModeHeight / node.Ctx.Size.Height);
                            scaledWidth = node.Ctx.Size.Width * fitScale * scale;
                            scaledHeight = node.Ctx.Size.Height * fitScale * scale;
                        }
                        else
                        {
                            scaledWidth = node.Ctx.Size.Width * scale;
                            scaledHeight = node.Ctx.Size.Height * scale;
                        }

                        node.Bounds.Width = scaledWidth;
                        node.Bounds.Height = scaledHeight;

                        node.Bounds.X = -scaledWidth / 2.0;
                        node.Bounds.Y = currentY;

                        currentY += (float)scaledHeight + spacing;
                        state.LayoutNodes.Add(node);
                    }
                }
            }
            else if (state.CurrentMode == ReadingMode.SinglePage)
            {
                lock (allNodes)
                {
                    if (currentPageIndex >= 0 && currentPageIndex < allNodes.Count)
                    {
                        var node = allNodes[currentPageIndex];
                        if (isFitToModeSize && cache.ModeWidth > 0 && cache.ModeHeight > 0)
                        {
                            double fitScale = Math.Min(cache.ModeWidth / node.Ctx.Size.Width, cache.ModeHeight / node.Ctx.Size.Height);
                            node.Bounds.Width = node.Ctx.Size.Width * fitScale;
                            node.Bounds.Height = node.Ctx.Size.Height * fitScale;
                        }
                        else if (node.IsSizeLoaded)
                        {
                            node.Bounds.Width = node.Ctx.Size.Width;
                            node.Bounds.Height = node.Ctx.Size.Height;
                        }

                        node.Bounds.X = -node.Bounds.Width / 2.0;
                        node.Bounds.Y = -node.Bounds.Height / 2.0;
                        state.LayoutNodes.Add(node);
                    }
                }
            }
            else if (state.CurrentMode == ReadingMode.SpreadRtl || state.CurrentMode == ReadingMode.SpreadLtr)
            {
                lock (allNodes)
                {
                    var nodesToAdd = new List<RenderNode>();
                    if (currentPageIndex == 0)
                    {
                        if (allNodes.Count > 0) nodesToAdd.Add(allNodes[0]);
                    }
                    else
                    {
                        // 双页模式按 1-2、3-4 进行配对，首页单独处理可避免封面被错误并页。
                        int pairStart = ((currentPageIndex - 1) / 2) * 2 + 1;

                        if (pairStart < allNodes.Count) nodesToAdd.Add(pairStart >= 0 ? allNodes[pairStart] : null!);
                        if (pairStart + 1 < allNodes.Count) nodesToAdd.Add(allNodes[pairStart + 1]);
                        nodesToAdd.RemoveAll(n => n == null);
                    }

                    CalculateSpreadNodeBounds(nodesToAdd, isFitToModeSize, cache);

                    if (nodesToAdd.Count == 1)
                    {
                        var node = nodesToAdd[0];
                        if (state.CurrentMode == ReadingMode.SpreadRtl)
                        {
                            node.Bounds.X = 0;
                        }
                        else
                        {
                            node.Bounds.X = -node.Bounds.Width;
                        }
                        node.Bounds.Y = -node.Bounds.Height / 2.0;
                        state.LayoutNodes.Add(node);
                    }
                    else if (nodesToAdd.Count == 2)
                    {
                        RenderNode left;
                        RenderNode right;

                        if (state.CurrentMode == ReadingMode.SpreadRtl)
                        {
                            right = nodesToAdd[0];
                            left = nodesToAdd[1];
                        }
                        else
                        {
                            left = nodesToAdd[0];
                            right = nodesToAdd[1];
                        }

                        // 双页默认贴合排布，保留 spacing 变量是为了后续可配置扩展。
                        float spacing = 0;

                        left.Bounds.X = -left.Bounds.Width - spacing / 2.0;
                        left.Bounds.Y = -left.Bounds.Height / 2.0;

                        right.Bounds.X = spacing / 2.0;
                        right.Bounds.Y = -right.Bounds.Height / 2.0;

                        state.LayoutNodes.Add(left);
                        state.LayoutNodes.Add(right);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 获取当前应使用的全局缩放值，必要时触发缓存重算。
    /// </summary>
    /// <param name="allNodes">全部页面节点集合。</param>
    /// <param name="viewSize">当前视口大小。</param>
    /// <param name="cache">布局缓存状态。</param>
    /// <returns>用于当前布局计算的缩放值。</returns>
    private float GetScale(List<RenderNode> allNodes, Vector2 viewSize, ReaderLayoutCacheState cache)
    {
        // 仅在关键维度变化时重算，避免每帧遍历节点带来的持续开销。
        bool needRecalculate = Math.Abs(viewSize.Y - cache.CachedViewHeight) > 1.0f
                               || Math.Abs(viewSize.X - cache.CachedViewWidth) > 1.0f
                               || Math.Abs(allNodes.Count - cache.CachedNodeCount) > 10;

        if (needRecalculate)
        {
            UpdateCachedScale(allNodes, viewSize, cache);
        }

        return cache.CachedScale;
    }

    /// <summary>
    /// 重算并更新缩放缓存。
    /// </summary>
    /// <param name="allNodes">全部页面节点集合。</param>
    /// <param name="viewSize">当前视口大小。</param>
    /// <param name="cache">布局缓存状态。</param>
    /// <returns>无返回值。</returns>
    private void UpdateCachedScale(List<RenderNode> allNodes, Vector2 viewSize, ReaderLayoutCacheState cache)
    {
        if (allNodes.Count == 0 || viewSize.Y <= 0 || viewSize.X <= 0)
        {
            cache.CachedScale = 1.0f;
            return;
        }

        // 只采样已完成尺寸探测的节点，避免占位尺寸污染众数统计结果。
        var loadedNodes = allNodes.Where(n => n.IsSizeLoaded).ToList();

        if (loadedNodes.Count == 0)
        {
            cache.CachedScale = 1.0f;
            return;
        }

        // 限制采样规模是为了在大图集场景下控制重算延迟。
        var sampleNodes = loadedNodes.Count <= 100 ? loadedNodes : loadedNodes.Take(100).ToList();

        var modeHeightGroup = sampleNodes
            .GroupBy(n => Math.Round(n.Ctx.Size.Height))
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var modeWidthGroup = sampleNodes
            .GroupBy(n => Math.Round(n.Ctx.Size.Width))
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (modeHeightGroup != null && modeWidthGroup != null)
        {
            cache.ModeHeight = modeHeightGroup.Key;
            cache.ModeWidth = modeWidthGroup.Key;

            if (cache.ModeHeight > 0 && cache.ModeWidth > 0)
            {
                // 取宽高适配中的较小值，确保页面不会超出可视区域。
                float scaleH = viewSize.Y / (float)cache.ModeHeight;
                float scaleW = viewSize.X / (float)cache.ModeWidth;

                cache.CachedScale = Math.Min(scaleH, scaleW);
            }
        }
        else
        {
            cache.ModeHeight = 0;
            cache.ModeWidth = 0;
        }

        if (Math.Abs(cache.CachedViewHeight - viewSize.Y) > 1f || Math.Abs(cache.CachedViewWidth - viewSize.X) > 1f)
        {
            Log.Debug(
                "UpdateCachedScale: ViewSize changed, new scale={CachedScale:F2}, viewSize=({ViewSizeX:F0}x{ViewSizeY:F0}), modeHeight={Key}, modeWidth={D}, sampleCount={SampleNodesCount}",
                cache.CachedScale,
                viewSize.X,
                viewSize.Y,
                modeHeightGroup?.Key,
                modeWidthGroup?.Key,
                sampleNodes.Count);
        }

        cache.CachedViewHeight = viewSize.Y;
        cache.CachedViewWidth = viewSize.X;
        cache.CachedNodeCount = allNodes.Count;
    }

    /// <summary>
    /// 计算双页模式下的节点尺寸。
    /// </summary>
    /// <param name="nodes">参与双页排版的节点集合。</param>
    /// <param name="isFitToModeSize">是否按众数尺寸统一拟合。</param>
    /// <param name="cache">布局缓存状态。</param>
    /// <returns>无返回值。</returns>
    private void CalculateSpreadNodeBounds(List<RenderNode> nodes, bool isFitToModeSize, ReaderLayoutCacheState cache)
    {
        if (isFitToModeSize && cache.ModeWidth > 0 && cache.ModeHeight > 0)
        {
            if (nodes.Count == 1)
            {
                var node = nodes[0];
                double fitScale = Math.Min(cache.ModeWidth / node.Ctx.Size.Width, cache.ModeHeight / node.Ctx.Size.Height);
                node.Bounds.Width = node.Ctx.Size.Width * fitScale;
                node.Bounds.Height = node.Ctx.Size.Height * fitScale;
            }
            else if (nodes.Count == 2)
            {
                var node1 = nodes[0];
                var node2 = nodes[1];

                double scale1 = cache.ModeHeight / node1.Ctx.Size.Height;
                double scale2 = cache.ModeHeight / node2.Ctx.Size.Height;

                double width1 = node1.Ctx.Size.Width * scale1;
                double width2 = node2.Ctx.Size.Width * scale2;

                // 当并页总宽超出双页目标宽度时整体等比收缩，保证并页仍完整落入视口。
                double combinedWidth = width1 + width2;
                if (combinedWidth > cache.ModeWidth * 2)
                {
                    double shrinkScale = (cache.ModeWidth * 2) / combinedWidth;
                    scale1 *= shrinkScale;
                    scale2 *= shrinkScale;
                }

                node1.Bounds.Width = node1.Ctx.Size.Width * scale1;
                node1.Bounds.Height = node1.Ctx.Size.Height * scale1;

                node2.Bounds.Width = node2.Ctx.Size.Width * scale2;
                node2.Bounds.Height = node2.Ctx.Size.Height * scale2;
            }
        }
        else
        {
            if (nodes.Count == 1)
            {
                var node = nodes[0];
                if (node.IsSizeLoaded)
                {
                    node.Bounds.Width = node.Ctx.Size.Width;
                    node.Bounds.Height = node.Ctx.Size.Height;
                }
            }
            else if (nodes.Count == 2)
            {
                var node1 = nodes[0];
                var node2 = nodes[1];

                if (node1.IsSizeLoaded && node2.IsSizeLoaded)
                {
                    // 在非拟合模式下统一到同一高度，可避免跨页视觉跳变。
                    double maxHeight = Math.Max(node1.Ctx.Size.Height, node2.Ctx.Size.Height);
                    double scale1 = maxHeight / node1.Ctx.Size.Height;
                    double scale2 = maxHeight / node2.Ctx.Size.Height;

                    node1.Bounds.Width = node1.Ctx.Size.Width * scale1;
                    node1.Bounds.Height = node1.Ctx.Size.Height * scale1;

                    node2.Bounds.Width = node2.Ctx.Size.Width * scale2;
                    node2.Bounds.Height = node2.Ctx.Size.Height * scale2;
                }
            }
        }
    }
}