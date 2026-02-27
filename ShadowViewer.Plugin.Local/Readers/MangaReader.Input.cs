using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Input;

namespace ShadowViewer.Plugin.Local.Readers;

public partial class MangaReader
{
    // --- 输入处理 ---


    /// <summary>
    /// 活跃的指针及其位置。
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<uint, Vector2> activePointers = new();

    /// <summary>
    /// 双指交互时的上一帧距离。
    /// </summary>
    private float lastPinchDistance = 0f;

    /// <summary>
    /// 指针移动增量累加，用于在 Update 中统一处理。
    /// </summary>
    private Vector2 pendingDelta = Vector2.Zero;

    /// <summary>
    /// 缩放增量累加。
    /// </summary>
    private float pendingZoomDelta = 1.0f;

    /// <summary>
    /// 缩放中心累加。
    /// </summary>
    private Vector2 pendingZoomCenter = Vector2.Zero;

    /// <summary>
    /// 上一次缩放中心，用于缩放惯性。
    /// </summary>
    private Vector2 lastZoomCenter = Vector2.Zero;

    private Vector2 dragStartPos;

    // --- 输入处理 ---

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
            
            lock (activePointers)
            {
                activePointers[id] = pos;
                
                if (activePointers.Count == 1)
                {
                    isDragging = true;
                    isUserInteracting = true;
                    dragStartPos = pos;
                    lastPointerPos = pos;
                    state.Velocity = Vector2.Zero;
                }
                else if (activePointers.Count == 2)
                {
                    // 获取两个点计算初始距离
                    var keys = new System.Collections.Generic.List<uint>(activePointers.Keys);
                    lastPinchDistance = Vector2.Distance(activePointers[keys[0]], activePointers[keys[1]]);
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

        lock (activePointers)
        {
            if (!activePointers.ContainsKey(id)) return;

            if (activePointers.Count == 1)
            {
                Vector2 delta = currentPos - activePointers[id];
                
                // 低通滤波/死区处理 (消除微小抖动)
                if (delta.LengthSquared() > 0.1f)
                {
                    pendingDelta += delta;
                }
            }
            else if (activePointers.Count == 2)
            {
                // 先更新当前点
                activePointers[id] = currentPos;
                
                // 计算新的缩放和中心点
                var keys = new System.Collections.Generic.List<uint>(activePointers.Keys);
                Vector2 p1 = activePointers[keys[0]];
                Vector2 p2 = activePointers[keys[1]];
                
                float currentDist = Vector2.Distance(p1, p2);
                if (lastPinchDistance > 0)
                {
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
        lock (activePointers)
        {
            if (activePointers.ContainsKey(id))
            {
                if (activePointers.Count == 1)
                {
                    // 最后一个手指离开，触发翻页或惯性
                    Vector2 currentPos = activePointers[id];
                    bool isZoomed = Math.Abs(state.Zoom - baseZoomScale) > 0.001f;
                    
                    if (!isZoomed && (state.CurrentMode is ReadingMode.SpreadLtr or ReadingMode.SpreadRtl))
                    {
                        var totalDelta = currentPos - dragStartPos;
                        // 综合位移与速度判断 (速度暂由上一帧计算)
                        bool isSwipe = Math.Abs(totalDelta.X) > 50 || Math.Abs(state.Velocity.X * state.Zoom) > 500;
                        
                        if (isSwipe && Math.Abs(totalDelta.X) > Math.Abs(totalDelta.Y))
                        {
                            int direction = totalDelta.X > 0 ? -1 : 1;
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

                    isDragging = false;
                    isUserInteracting = false;
                    state.Velocity = -pendingDelta / 0.016f; // 初始速度方案
                }
                
                activePointers.Remove(id);
                lastPinchDistance = 0;
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