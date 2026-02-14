using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Input;

namespace ShadowViewer.Plugin.Local.Readers;

public partial class MangaReader
{
    // --- 输入处理 ---

    /// <summary>
    /// 指针按下处理：开始拖拽并捕获指针以实现平移交互。
    /// </summary>
    private void MainCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(mainCanvas);
        if (point.Properties.IsLeftButtonPressed)
        {
            isDragging = true;
            isUserInteracting = true;  // 标记用户开始交互
            pointerId = (int)point.PointerId;
            lastPointerPos = point.Position.ToVector2();
            state.Velocity = Vector2.Zero;
            mainCanvas?.CapturePointer(e.Pointer);
        }
    }

    /// <summary>
    /// 指针移动处理：在拖拽期间更新摄像机位置并计算简单速度用于惯性。
    /// </summary>
    private void MainCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (isDragging && e.Pointer.PointerId == pointerId)
        {
            var point = e.GetCurrentPoint(mainCanvas);
            var currentPos = point.Position.ToVector2();
            var delta = currentPos - lastPointerPos;

            state.CameraPos -= delta / state.Zoom;

            // 简单的速度计算
            state.Velocity = -(delta / state.Zoom) / 0.016f; // 假设 60fps

            lastPointerPos = currentPos;
        }
    }

    /// <summary>
    /// 指针释放处理：结束拖拽并释放指针捕获。
    /// </summary>
    private void MainCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId == pointerId)
        {
            isDragging = false;
            isUserInteracting = false;  // 标记用户结束交互
            pointerId = -1;
            mainCanvas?.ReleasePointerCapture(e.Pointer);
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
            
            // 延迟清除交互标记，避免立即被布局更新打断
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);  // 滚轮停止后500ms清除标记
                isUserInteracting = false;
            });
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

}