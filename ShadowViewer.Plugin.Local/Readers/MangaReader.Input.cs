using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Input;
using ShadowViewer.Plugin.Local.Readers.Internal;

namespace ShadowViewer.Plugin.Local.Readers;

public partial class MangaReader
{
    // --- 输入处理 ---

    /// <summary>
    /// 滚轮交互去抖任务取消源，用于避免旧任务覆盖最新交互状态。
    /// </summary>
    private CancellationTokenSource? wheelInteractionCts;

    /// <summary>
    /// 同步滚轮去抖取消源替换过程的锁对象。
    /// </summary>
    private readonly object wheelInteractionLock = new();

    /// <summary>
    /// 输入控制器，负责指针状态与增量计算。
    /// </summary>
    private readonly ReaderInputController inputController = new();

    /// <summary>
    /// 指针按下处理：开始拖拽并捕获指针以实现平移交互。
    /// </summary>
    private void MainCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(mainCanvas);
        if (point.Properties.IsLeftButtonPressed || e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch)
        {
            uint id = point.PointerId;
            Vector2 pos = point.Position.ToVector2();

            bool isPrimaryPointer = inputController.TryHandlePointerPressed(
                id,
                pos,
                isAnimatingPageTurn,
                pageTurnAnimCurlAmount,
                state.Zoom,
                pageTurnCurlFromRight);

            if (isPrimaryPointer)
            {
                CancelWheelInteractionClear();
                isDragging = true;
                isUserInteracting = true;
                lastPointerPos = pos;
                state.Velocity = Vector2.Zero;

                if (isAnimatingPageTurn)
                {
                    isAnimatingPageTurn = false;
                }
            }

            mainCanvas?.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 指针移动处理：记录位置变化，统一在 Update 循环中应用。
    /// </summary>
    private void MainCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        uint id = e.Pointer.PointerId;
        var point = e.GetCurrentPoint(mainCanvas);
        Vector2 currentPos = point.Position.ToVector2();

        inputController.HandlePointerMoved(id, currentPos);
    }

    /// <summary>
    /// 指针释放处理：结束交互并触发翻页检测。
    /// </summary>
    private void MainCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        HandlePointerLost(e.Pointer.PointerId);
    }

    private void MainCanvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        HandlePointerLost(e.Pointer.PointerId);
    }

    private void MainCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        HandlePointerLost(e.Pointer.PointerId);
    }

    private void HandlePointerLost(uint id)
    {
        var snapshot = inputController.HandlePointerLost(id);
        if (!snapshot.IsTrackedPointer)
        {
            return;
        }

        if (snapshot.IsLastPointerLost)
        {
            // 最后一个手指离开，触发翻页或惯性
            Vector2 currentPos = snapshot.PointerPosition;
            bool isZoomed = Math.Abs(state.Zoom - baseZoomScale) > 0.001f;

            if (!isZoomed && (state.CurrentMode is ReadingMode.SpreadLtr or ReadingMode.SpreadRtl))
            {
                var totalDelta = currentPos - inputController.DragStartPos;

                // 将翻页判定集中到服务层，避免阈值和目标页策略散落在输入事件中。
                PageTurnPlan pageTurnPlan;
                bool hasPageTurnPlan;
                lock (state.LayoutNodes)
                {
                    var request = new PageTurnRequest(
                        totalDelta,
                        state.Velocity.X,
                        state.Zoom,
                        CurrentPageIndex,
                        TotalPage,
                        state.CurrentMode,
                        state.LayoutNodes);

                    hasPageTurnPlan = pageTurnService.TryCreatePlan(request, out pageTurnPlan);
                }

                if (hasPageTurnPlan)
                {
                    isAnimatingPageTurn = true;
                    pageTurnTargetIndex = pageTurnPlan.TargetPageIndex;
                    pageTurnCurlFromRight = pageTurnPlan.CurlFromRight;
                    pageTurnAnimCurlAmount = pageTurnPlan.CurrentCurl;
                    pageTurnCurlingNode = pageTurnPlan.CurlingNode;
                    pageTurnAnimTargetCurl = pageTurnPlan.TargetCurl;
                    pageTurnAnimVelocity = pageTurnPlan.AnimVelocity;
                }
            }

            isDragging = false;
            isUserInteracting = false;
            if (!isAnimatingPageTurn)
            {
                state.Velocity = -snapshot.PendingPanDelta / 0.016f; // 初始速度方案
            }
            else
            {
                state.Velocity = Vector2.Zero;
            }
        }
    }

    /// <summary>
    /// 鼠标滚轮处理：按住 Ctrl 时进行缩放，否则在滚动模式下滚动视图，在分页模式下可用于翻页。
    /// </summary>
    private void MainCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(mainCanvas);
        var delta = point.Properties.MouseWheelDelta;
        bool isCtrlPressed = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);

        if (isCtrlPressed)
        {
            var screenPoint = point.Position.ToVector2();
            var center = viewSize / 2;

            float oldZoom = state.Zoom;
            float zoomFactor = delta > 0 ? 1.1f : 0.9f;

            float minZoom = 0.1f * baseZoomScale;
            float maxZoom = 5.0f * baseZoomScale;

            float newZoom = Math.Clamp(state.Zoom * zoomFactor, minZoom, maxZoom);
            if (Math.Abs(newZoom - oldZoom) < 0.0001f) return;

            state.Zoom = newZoom;

            // 缩放补偿：鼠标下的点在世界坐标中的位置不变
            // World = (Screen - Center) / Zoom + Camera
            // (Screen - Center) / OldZoom + OldCamera = (Screen - Center) / NewZoom + NewCamera
            // NewCamera = OldCamera + (Screen - Center) * (1/OldZoom - 1/NewZoom)

            state.CameraPos += (screenPoint - center) * (1.0f / oldZoom - 1.0f / state.Zoom);
        }
        else if (state.CurrentMode == ReadingMode.VerticalScroll)
        {
            // 停止惯性
            state.Velocity = Vector2.Zero;
            // 滚轮滚动
            isUserInteracting = true;  // 标记用户正在滚动
            state.CameraPos.Y -= delta / state.Zoom;

            ScheduleWheelInteractionClear();
        }
        else if (EnableMouseWheelNavigation)
        {
            // 单页或双页模式：滚轮翻页
            if (delta != 0)
            {
                int direction = delta < 0 ? 1 : -1;
                int step = (state.CurrentMode == ReadingMode.SinglePage) ? 1 : 2;

                int target = CurrentPageIndex + (direction * step);

                if (target < 0) target = 0;
                if (target >= TotalPage) target = TotalPage > 0 ? TotalPage - 1 : 0;

                if (target != CurrentPageIndex)
                {
                    CurrentPageIndex = target;
                }
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// 调度滚轮交互结束后的去抖清理，防止布局刷新过早夺回摄像机控制。
    /// </summary>
    private void ScheduleWheelInteractionClear()
    {
        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(deferredUiWorkCts.Token);
        CancellationTokenSource? oldCts;

        lock (wheelInteractionLock)
        {
            oldCts = wheelInteractionCts;
            wheelInteractionCts = linkedCts;
        }

        oldCts?.Cancel();
        oldCts?.Dispose();

        _ = ClearWheelInteractionAsync(linkedCts.Token);
    }

    /// <summary>
    /// 取消并释放滚轮交互去抖任务。
    /// </summary>
    private void CancelWheelInteractionClear()
    {
        CancellationTokenSource? oldCts;

        lock (wheelInteractionLock)
        {
            oldCts = wheelInteractionCts;
            wheelInteractionCts = null;
        }

        oldCts?.Cancel();
        oldCts?.Dispose();
    }

    /// <summary>
    /// 异步等待滚轮空闲窗口并清除交互标记。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步清理流程的任务。</returns>
    private async Task ClearWheelInteractionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            isUserInteracting = false;
        }
        catch (OperationCanceledException)
        {
            // 新交互开始或控件卸载时取消属于正常行为。
        }
    }

}