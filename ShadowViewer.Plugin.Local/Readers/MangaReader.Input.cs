using System;
using System.Numerics;
using Microsoft.UI.Xaml.Input;
using ShadowViewer.Plugin.Local.Readers.Internal;

namespace ShadowViewer.Plugin.Local.Readers;

public partial class MangaReader
{
    // --- 输入处理 ---

    /// <summary>
    /// 输入控制器，负责指针状态与增量计算。
    /// </summary>
    private readonly ReaderInputController inputController = new();
    private bool dragStartedAtTop;
    private bool dragStartedAtBottom;

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
                pageTurnCurlFromRight,
                pageTurnCurlTilt);

            if (isPrimaryPointer)
            {
                InterruptPageTurn();
                isDragging = true;
                isUserInteracting = true;
                lastPointerPos = pos;
                state.Velocity = Vector2.Zero;
                dragStartedAtTop = ReaderNavigationService.IsScrollBoundary(state, viewSize, -1);
                dragStartedAtBottom = ReaderNavigationService.IsScrollBoundary(state, viewSize, 1);
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
        inputController.HandlePointerMoved(e.Pointer.PointerId, e.GetCurrentPoint(mainCanvas).Position.ToVector2());
        HandlePointerLost(e.Pointer.PointerId);
    }

    private void MainCanvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        HandlePointerLost(e.Pointer.PointerId, canceled: true);
    }

    private void MainCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        HandlePointerLost(e.Pointer.PointerId, canceled: true);
    }

    private void HandlePointerLost(uint id, bool canceled = false)
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
            bool handledNavigationGesture = false;

            if (!isZoomed && snapshot.IsPageTurnGesture &&
                (state.CurrentMode is ReadingMode.SpreadLtr or ReadingMode.SpreadRtl))
            {
                var totalDelta = currentPos - inputController.DragStartPos;

                // 将翻页判定集中到服务层，避免阈值和目标页策略散落在输入事件中。
                PageTurnPlan pageTurnPlan;
                bool hasPageTurnPlan;
                lock (state.LayoutNodes)
                {
                    var request = new PageTurnRequest(
                        totalDelta,
                        snapshot.Velocity.X / state.Zoom,
                        state.Zoom,
                        CurrentPageIndex,
                        TotalPage,
                        state.CurrentMode,
                        state.LayoutNodes,
                        canceled);

                    hasPageTurnPlan = pageTurnService.TryCreatePlan(request, out pageTurnPlan);
                }

                if (hasPageTurnPlan)
                {
                    StartPageTurn(pageTurnPlan);
                    if (pageTurnPlan.ChapterDirection != 0)
                        RequestChapterNavigation(pageTurnPlan.ChapterDirection);
                }
            }
            else if (!isZoomed && snapshot.IsPageTurnGesture)
            {
                var totalDelta = currentPos - inputController.DragStartPos;
                if (state.CurrentMode == ReadingMode.SinglePage)
                {
                    int direction = ReaderNavigationService.GetSwipeDirection(
                        totalDelta, snapshot.Velocity.X, state.CurrentMode, canceled);
                    if (direction != 0)
                    {
                        TurnPage(direction);
                        handledNavigationGesture = true;
                    }
                }
                else if (state.CurrentMode == ReadingMode.VerticalScroll)
                {
                    int direction = ReaderNavigationService.GetScrollSwipeDirection(
                        totalDelta, dragStartedAtTop, dragStartedAtBottom, canceled);
                    if (direction != 0 && ReaderNavigationService.IsScrollBoundary(state, viewSize, direction))
                    {
                        RequestChapterNavigation(direction);
                        handledNavigationGesture = true;
                    }
                }
            }

            isDragging = false;
            isUserInteracting = false;
            state.ZoomVelocity = 0;
            if (!isAnimatingPageTurn)
            {
                bool isSpread = state.CurrentMode is ReadingMode.SpreadLtr or ReadingMode.SpreadRtl;
                state.Velocity = canceled || handledNavigationGesture || (!isZoomed && isSpread && snapshot.IsPageTurnGesture)
                    ? Vector2.Zero : -snapshot.Velocity / state.Zoom;
                if (!isZoomed && state.CurrentMode == ReadingMode.VerticalScroll && !allowHorizontalDragInScrollMode)
                {
                    state.Velocity.X = 0;
                }
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
            CancelPageTurn(resetInput: true);
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
            if (isDragging)
            {
                e.Handled = true;
                return;
            }
            // 停止惯性
            state.Velocity = Vector2.Zero;
            int direction = Math.Sign(-delta);
            if (Math.Abs(state.Zoom - baseZoomScale) <= 0.001f &&
                ReaderNavigationService.IsScrollBoundary(state, viewSize, direction))
            {
                RequestChapterNavigation(direction);
                e.Handled = true;
                return;
            }
            // 滚轮位移在帧循环中平滑应用，避免每个刻度直接跳动相机。
            isUserInteracting = true;  // 标记用户正在滚动
            inputController.HandleWheel(delta);

        }
        else if (EnableMouseWheelNavigation)
        {
            // 单页或双页模式：滚轮翻页
            if (delta != 0)
            {
                TurnPage(delta < 0 ? 1 : -1);
            }
        }

        e.Handled = true;
    }

}
