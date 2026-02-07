using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 漫画阅读器控件（缩放相关部分）。负责管理缩放状态、信息面板显示以及与缩放 UI 元件的交互。
/// </summary>
public sealed partial class MangaReader
{
    /// <summary>
    /// 信息面板，用于显示当前缩放与位置等信息的容器。
    /// </summary>
    private Border? infoPanel;

    /// <summary>
    /// 缩放滑动条（UI 元件）。
    /// </summary>
    private Slider? zoomSlider;

    /// <summary>
    /// 显示缩放百分比的文本块。
    /// </summary>
    private TextBlock? zoomText;

    /// <summary>
    /// 显示当前位置的文本块。
    /// </summary>
    private TextBlock? positionText;

    /// <summary>
    /// 重置缩放的按钮。
    /// </summary>
    private Button? resetButton;

    /// <summary>
    /// 指示是否正在由代码更新缩放 UI，以避免循环触发事件处理。
    /// </summary>
    private bool isUpdatingZoomUi;

    /// <summary>
    /// 上次记录的信息面板缩放值（用于变化检测）。
    /// </summary>
    private float lastPanelZoom = -1;

    /// <summary>
    /// 上次记录的信息面板位置（用于变化检测）。
    /// </summary>
    private Vector2 lastPanelPos = new(-10000, -10000);

    /// <summary>
    /// 上次记录的信息面板可见性（用于避免不必要的 UI 更新）。
    /// </summary>
    private bool? lastPanelVisible;

    /// <summary>
    /// 更新信息面板的显示状态与内容。当缩放或位置发生显著变化时，会将更新调度到 UI 线程。
    /// 该方法包含性能优化：只有在可见性变化或数值变化超过阈值时才会更新 UI 元件。
    /// </summary>
    private void UpdateInfoPanel()
    {
        if (infoPanel == null) return;

        // Check if values changed significantly enough to warrant a UI update
        float currentZoom = state.Zoom;
        Vector2 currentPos = state.CameraPos;

        bool isZoomed = Math.Abs(currentZoom - baseZoomScale) > 0.001f;
        bool isMoved;

        if (state.CurrentMode == ReadingMode.Scroll)
        {
            // In Scroll mode, ignore Y changes for visibility
            isMoved = Math.Abs(currentPos.X) > 1.0f;
        }
        else
        {
            isMoved = currentPos.LengthSquared() > 1.0f;
        }

        bool shouldShow = isZoomed || isMoved;

        // Optimization: Only dispatch if visibility changes or values change significantly while visible
        bool visibilityChanged = lastPanelVisible != shouldShow;
        bool valuesChanged = shouldShow && (Math.Abs(currentZoom - lastPanelZoom) > 0.01f ||
                                            Vector2.DistanceSquared(currentPos, lastPanelPos) > 1.0f);

        if (visibilityChanged || valuesChanged)
        {
            lastPanelVisible = shouldShow;
            lastPanelZoom = currentZoom;
            lastPanelPos = currentPos;

            float zoomPercent = (baseZoomScale > 0) ? (currentZoom / baseZoomScale) * 100 : 100;
            // Clamp for display
            if (zoomPercent < 0) zoomPercent = 0;

            string posText = $"X: {currentPos.X:F0}, Y: {currentPos.Y:F0}";

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (infoPanel != null)
                {
                    if (infoPanel.Visibility == Visibility.Visible != shouldShow)
                    {
                        infoPanel.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                    }

                    if (shouldShow)
                    {
                        if (zoomSlider != null)
                        {
                            isUpdatingZoomUi = true;
                            zoomSlider.Value = zoomPercent;
                            isUpdatingZoomUi = false;
                        }

                        if (zoomText != null) zoomText.Text = $"{zoomPercent:F0}%";
                        if (positionText != null) positionText.Text = posText;
                    }
                }
            });
        }
    }

    /// <summary>
    /// 从控件模板中获取与缩放信息面板相关的模板部分并绑定交互事件（例如滑动条与重置按钮）。
    /// 在控件模板应用后应该调用此方法以获得对这些 UI 元件的引用并设置事件处理器。
    /// </summary>
    private void OnApplyZoomFlyoutTemplate()
    {
        infoPanel = GetTemplateChild("PART_InfoPanel") as Border;
        zoomSlider = GetTemplateChild("PART_ZoomSlider") as Slider;
        zoomText = GetTemplateChild("PART_ZoomText") as TextBlock;
        positionText = GetTemplateChild("PART_PositionText") as TextBlock;
        resetButton = GetTemplateChild("PART_ResetButton") as Button;

        if (zoomSlider != null)
        {
            zoomSlider.ValueChanged += (_, e) =>
            {
                if (isUpdatingZoomUi) return;

                float newScale = (float)e.NewValue / 100.0f;
                // Ensure we have a valid base scale
                float baseScale = baseZoomScale > 0 ? baseZoomScale : 1.0f;
                state.Zoom = newScale * baseScale;
            };
        }

        if (resetButton != null)
        {
            resetButton.Click += (_, _) => ResetZoom();
        }
    }

    /// <summary>
    /// 将缩放重置为默认状态并根据当前阅读模式重新定位摄像机与基础缩放比例。
    /// 对于滚动模式会尽量保持 X 轴为居中；对于分页模式会根据内容边界计算合适的基础缩放使内容适配视口。
    /// </summary>
    public void ResetZoom()
    {
        state.Zoom = 1.0f;
        state.Velocity = Vector2.Zero;
        baseZoomScale = 1.0f;

        // 滚动模式下，X=0 即为中心
        if (state.CurrentMode == ReadingMode.Scroll)
        {
            state.CameraPos.X = 0;

            int total;
            lock (state.LayoutNodes)
            {
                total = state.LayoutNodes.Count;
            }

            if (total > 0)
            {
                if (CurrentPageIndex == 0)
                {
                    ScrollToPage(0);
                }
                else if (CurrentPageIndex == total - 1)
                {
                    ScrollToPage(total - 1);
                }
            }
        }
        else
        {
            // Single/Spread always centered at 0,0
            state.CameraPos = Vector2.Zero;

            if (viewSize is { X: > 0, Y: > 0 })
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                bool hasNodes = false;

                lock (state.LayoutNodes)
                {
                    foreach (var node in state.LayoutNodes)
                    {
                        hasNodes = true;
                        if (node.Bounds.X < minX) minX = node.Bounds.X;
                        if (node.Bounds.Y < minY) minY = node.Bounds.Y;
                        if (node.Bounds.X + node.Bounds.Width > maxX) maxX = node.Bounds.X + node.Bounds.Width;
                        if (node.Bounds.Y + node.Bounds.Height > maxY) maxY = node.Bounds.Y + node.Bounds.Height;
                    }
                }

                if (hasNodes)
                {
                    // Calculate width/height based on distance from center (0,0)
                    // This ensures the center point (Gap or Image Center) remains at screen center
                    double maxDistX = Math.Max(Math.Abs(minX), Math.Abs(maxX));
                    double maxDistY = Math.Max(Math.Abs(minY), Math.Abs(maxY));

                    double contentWidth = maxDistX * 2;
                    double contentHeight = maxDistY * 2;

                    if (contentWidth > 0 && contentHeight > 0)
                    {
                        float scaleX = viewSize.X / (float)contentWidth;
                        float scaleY = viewSize.Y / (float)contentHeight;
                        baseZoomScale = Math.Min(scaleX, scaleY);
                        state.Zoom = baseZoomScale;
                    }
                }
            }
        }
    }
}