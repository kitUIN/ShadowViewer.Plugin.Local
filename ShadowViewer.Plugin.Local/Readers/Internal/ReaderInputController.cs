using System.Collections.Generic;
using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>
/// 表示一帧内收集到的输入增量数据（平移与缩放）。
/// </summary>
internal readonly struct InputFrameDelta
{
    /// <summary>
    /// 初始化 <see cref="InputFrameDelta"/> 的新实例。
    /// </summary>
    /// <param name="panDelta">本帧累计平移增量（屏幕坐标）。</param>
    /// <param name="zoomDelta">本帧累计缩放倍率。</param>
    /// <param name="zoomCenter">本帧缩放中心点（屏幕坐标）。</param>
    /// <param name="hasPointer">是否存在活跃指针。</param>
    /// <param name="pointerPos">任意一个活跃指针位置（用于拖拽参考）。</param>
    public InputFrameDelta(Vector2 panDelta, float zoomDelta, Vector2 zoomCenter, bool hasPointer, Vector2 pointerPos)
    {
        PanDelta = panDelta;
        ZoomDelta = zoomDelta;
        ZoomCenter = zoomCenter;
        HasActivePointer = hasPointer;
        ActivePointerPos = pointerPos;
    }

    /// <summary>
    /// 获取本帧累计平移增量。
    /// </summary>
    public Vector2 PanDelta { get; }

    /// <summary>
    /// 获取本帧累计缩放倍率。
    /// </summary>
    public float ZoomDelta { get; }

    /// <summary>
    /// 获取本帧缩放中心点。
    /// </summary>
    public Vector2 ZoomCenter { get; }

    /// <summary>
    /// 获取是否存在活跃指针。
    /// </summary>
    public bool HasActivePointer { get; }

    /// <summary>
    /// 获取任意一个活跃指针位置。
    /// </summary>
    public Vector2 ActivePointerPos { get; }
}

/// <summary>
/// 表示指针丢失（释放/取消）时的状态快照。
/// </summary>
internal readonly struct PointerLostSnapshot
{
    /// <summary>
    /// 初始化 <see cref="PointerLostSnapshot"/> 的新实例。
    /// </summary>
    /// <param name="tracked">当前指针是否被控制器跟踪。</param>
    /// <param name="lastPointerLost">是否为最后一个活跃指针离开。</param>
    /// <param name="pointerPos">离开时指针位置。</param>
    /// <param name="pendingPanDelta">当前尚未消费的平移增量。</param>
    public PointerLostSnapshot(bool tracked, bool lastPointerLost, Vector2 pointerPos, Vector2 pendingPanDelta)
    {
        IsTrackedPointer = tracked;
        IsLastPointerLost = lastPointerLost;
        PointerPosition = pointerPos;
        PendingPanDelta = pendingPanDelta;
    }

    /// <summary>
    /// 获取当前指针是否被跟踪。
    /// </summary>
    public bool IsTrackedPointer { get; }

    /// <summary>
    /// 获取是否为最后一个指针离开。
    /// </summary>
    public bool IsLastPointerLost { get; }

    /// <summary>
    /// 获取离开时指针位置。
    /// </summary>
    public Vector2 PointerPosition { get; }

    /// <summary>
    /// 获取尚未消费的平移增量。
    /// </summary>
    public Vector2 PendingPanDelta { get; }
}

/// <summary>
/// 阅读器输入控制器，负责管理指针集合并输出帧级输入增量。
/// </summary>
internal sealed class ReaderInputController
{
    /// <summary>
    /// 活跃指针集合，键为指针 ID，值为当前屏幕坐标。
    /// </summary>
    private readonly Dictionary<uint, Vector2> activePointers = new();

    /// <summary>
    /// 双指缩放时上一帧指距，用于计算缩放倍率。
    /// </summary>
    private float lastPinchDistance;

    /// <summary>
    /// 待消费的平移增量缓存。
    /// </summary>
    private Vector2 pendingDelta = Vector2.Zero;

    /// <summary>
    /// 待消费的缩放倍率缓存。
    /// </summary>
    private float pendingZoomDelta = 1.0f;

    /// <summary>
    /// 待消费的缩放中心缓存。
    /// </summary>
    private Vector2 pendingZoomCenter = Vector2.Zero;

    /// <summary>
    /// 获取当前拖拽起点（屏幕坐标）。
    /// </summary>
    public Vector2 DragStartPos { get; private set; } = Vector2.Zero;

    /// <summary>
    /// 获取或设置最后一次有效缩放中心（用于缩放惯性补偿）。
    /// </summary>
    public Vector2 LastZoomCenter { get; set; } = Vector2.Zero;

    /// <summary>
    /// 获取当前活跃指针数量。
    /// </summary>
    /// <returns>活跃指针数量。</returns>
    public int ActivePointerCount
    {
        get
        {
            lock (activePointers)
            {
                return activePointers.Count;
            }
        }
    }

    /// <summary>
    /// 处理指针按下事件。
    /// </summary>
    /// <param name="id">指针 ID。</param>
    /// <param name="pos">指针位置。</param>
    /// <param name="wasAnimatingPageTurn">按下时是否处于翻页动画中。</param>
    /// <param name="pageTurnAnimCurlAmount">当前翻页卷曲量。</param>
    /// <param name="currentZoom">当前缩放值。</param>
    /// <param name="pageTurnCurlFromRight">当前卷曲方向是否从右侧开始。</param>
    /// <returns>若本次按下成为首个活跃指针则返回 <c>true</c>。</returns>
    public bool TryHandlePointerPressed(uint id, Vector2 pos, bool wasAnimatingPageTurn, float pageTurnAnimCurlAmount, float currentZoom, bool pageTurnCurlFromRight)
    {
        lock (activePointers)
        {
            activePointers[id] = pos;

            if (activePointers.Count == 1)
            {
                if (wasAnimatingPageTurn)
                {
                    // 为了在动画被打断时保持卷曲连续性，需要回推拖拽起点而不是直接用当前坐标。
                    float currentDragDelta = pageTurnAnimCurlAmount * currentZoom;
                    if (pageTurnCurlFromRight)
                    {
                        currentDragDelta = -currentDragDelta;
                    }

                    DragStartPos = new Vector2(pos.X - currentDragDelta, pos.Y);
                }
                else
                {
                    DragStartPos = pos;
                }

                return true;
            }

            if (activePointers.Count == 2)
            {
                // 双指刚形成时记录初始距离，后续移动按距离比值累乘更稳定。
                var keys = new List<uint>(activePointers.Keys);
                lastPinchDistance = Vector2.Distance(activePointers[keys[0]], activePointers[keys[1]]);
            }

            return false;
        }
    }

    /// <summary>
    /// 处理指针移动事件并累加本帧输入增量。
    /// </summary>
    /// <param name="id">指针 ID。</param>
    /// <param name="currentPos">当前指针位置。</param>
    /// <returns>无返回值。</returns>
    public void HandlePointerMoved(uint id, Vector2 currentPos)
    {
        lock (activePointers)
        {
            if (!activePointers.ContainsKey(id))
            {
                return;
            }

            if (activePointers.Count == 1)
            {
                Vector2 delta = currentPos - activePointers[id];
                // 通过微小死区过滤抖动，避免高频小位移造成相机噪声。
                if (delta.LengthSquared() > 0.1f)
                {
                    pendingDelta += delta;
                }
            }
            else if (activePointers.Count == 2)
            {
                activePointers[id] = currentPos;

                var keys = new List<uint>(activePointers.Keys);
                Vector2 p1 = activePointers[keys[0]];
                Vector2 p2 = activePointers[keys[1]];

                float currentDist = Vector2.Distance(p1, p2);
                if (lastPinchDistance > 0)
                {
                    // 使用倍率累乘而非直接覆盖，能更好保留一帧内多次指针事件的总效果。
                    float deltaScale = currentDist / lastPinchDistance;
                    pendingZoomDelta *= deltaScale;
                    pendingZoomCenter = (p1 + p2) / 2f;
                }

                lastPinchDistance = currentDist;
            }

            activePointers[id] = currentPos;
        }
    }

    /// <summary>
    /// 消费并重置当前帧输入增量。
    /// </summary>
    /// <returns>包含平移、缩放和活跃指针信息的帧增量快照。</returns>
    public InputFrameDelta ConsumeFrameDelta()
    {
        lock (activePointers)
        {
            Vector2 panDelta = pendingDelta;
            pendingDelta = Vector2.Zero;

            float zoomDelta = pendingZoomDelta;
            pendingZoomDelta = 1.0f;

            Vector2 zoomCenter = pendingZoomCenter;

            if (activePointers.Count > 0)
            {
                var enumerator = activePointers.Values.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    return new InputFrameDelta(panDelta, zoomDelta, zoomCenter, true, enumerator.Current);
                }
            }

            return new InputFrameDelta(panDelta, zoomDelta, zoomCenter, false, Vector2.Zero);
        }
    }

    /// <summary>
    /// 处理指针丢失事件并返回释放时快照。
    /// </summary>
    /// <param name="id">丢失的指针 ID。</param>
    /// <returns>用于上层决定是否触发惯性或翻页的快照数据。</returns>
    public PointerLostSnapshot HandlePointerLost(uint id)
    {
        lock (activePointers)
        {
            if (!activePointers.ContainsKey(id))
            {
                return new PointerLostSnapshot(false, false, Vector2.Zero, Vector2.Zero);
            }

            bool isLastPointerLost = activePointers.Count == 1;
            Vector2 pointerPos = activePointers[id];
            Vector2 panDelta = pendingDelta;

            // 指针集合状态在此一次性更新，保证上层读取到的快照与后续状态一致。
            activePointers.Remove(id);
            lastPinchDistance = 0;

            return new PointerLostSnapshot(true, isLastPointerLost, pointerPos, panDelta);
        }
    }
}