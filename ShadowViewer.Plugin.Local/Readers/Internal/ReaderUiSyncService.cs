using System;
using System.Collections.Generic;
using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>
/// UI 同步服务，负责从引擎状态计算页码与缩放的 UI 快照。
/// </summary>
internal sealed class ReaderUiSyncService
{
    /// <summary>
    /// 尝试计算当前帧需要同步到 UI 的状态。
    /// </summary>
    /// <param name="isLayoutUpdating">是否正处于布局更新流程。</param>
    /// <param name="layoutNodes">当前布局节点集合。</param>
    /// <param name="totalNodes">总节点数量。</param>
    /// <param name="cameraPos">摄像机中心位置。</param>
    /// <param name="currentZoom">当前缩放值。</param>
    /// <param name="baseZoomScale">基准缩放值。</param>
    /// <param name="lastReportedZoom">上次上报的相对缩放。</param>
    /// <param name="lastReportedPage">上次上报的当前页。</param>
    /// <param name="lastReportedTotal">上次上报的总页数。</param>
    /// <param name="snapshot">输出的 UI 快照。</param>
    /// <returns>存在变化且需要同步时返回 <c>true</c>。</returns>
    public bool TryCreateSnapshot(
        bool isLayoutUpdating,
        IReadOnlyList<RenderNode> layoutNodes,
        int totalNodes,
        Vector2 cameraPos,
        float currentZoom,
        float baseZoomScale,
        float lastReportedZoom,
        int lastReportedPage,
        int lastReportedTotal,
        out ReaderUiStateSnapshot snapshot)
    {
        snapshot = default;

        if (isLayoutUpdating || layoutNodes.Count == 0)
        {
            return false;
        }

        int current = 1;
        float minDistance = float.MaxValue;
        RenderNode? closestNode = null;

        foreach (var node in layoutNodes)
        {
            var centerX = (float)(node.Bounds.X + node.Bounds.Width / 2);
            var centerY = (float)(node.Bounds.Y + node.Bounds.Height / 2);
            var center = new Vector2(centerX, centerY);

            float dist = Vector2.DistanceSquared(center, cameraPos);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestNode = node;
            }
        }

        if (closestNode != null)
        {
            current = closestNode.PageIndex + 1;
        }

        float relativeZoom = baseZoomScale > 0 ? currentZoom / baseZoomScale : 1.0f;

        bool zoomChanged = Math.Abs(relativeZoom - lastReportedZoom) > 0.001f;
        bool pageChanged = current != lastReportedPage || totalNodes != lastReportedTotal;

        if (!zoomChanged && !pageChanged)
        {
            return false;
        }

        snapshot = new ReaderUiStateSnapshot(current, totalNodes, relativeZoom);
        return true;
    }
}

/// <summary>
/// 单帧 UI 同步快照。
/// </summary>
/// <param name="CurrentPage">当前页（从 1 开始）。</param>
/// <param name="TotalPage">总页数。</param>
/// <param name="RelativeZoom">相对基准缩放倍率。</param>
internal readonly record struct ReaderUiStateSnapshot(int CurrentPage, int TotalPage, float RelativeZoom);
