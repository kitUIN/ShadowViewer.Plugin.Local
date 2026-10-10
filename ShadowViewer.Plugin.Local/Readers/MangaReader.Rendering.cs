using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers.Rendering;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers;

public sealed partial class MangaReader
{
    /// <summary>
    /// 将相机、布局和卷页状态作为同一帧读取，避免翻页完成时混用旧布局和新状态。
    /// </summary>
    private void DrawFrame(CanvasDrawingSession ds)
    {
        lock (pageTurnLock)
        {
            if (isPreparingPages) return;
            var size = viewSize;
            if (size == Vector2.Zero) return;

            var center = size / 2;
            var transform = Matrix3x2.CreateTranslation(-state.CameraPos) *
                            Matrix3x2.CreateScale(state.Zoom) *
                            Matrix3x2.CreateTranslation(center);

            ds.Transform = transform;

            // 计算视口 (用于剔除)
            if (!Matrix3x2.Invert(transform, out var inverseTransform)) return;
            var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
            var bottomRight = Vector2.Transform(size, inverseTransform);
            var viewportRect = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

            List<RenderNode> layoutSnapshot;
            lock (state.LayoutNodes)
            {
                layoutSnapshot = state.LayoutNodes.ToList();
            }

            List<RenderNode> allNodesSnapshot;
            lock (allNodes)
            {
                allNodesSnapshot = allNodes.ToList();
            }

            var renderContext = new PageRenderContext(
                ds,
                viewportRect,
                state.CurrentMode,
                state.Zoom,
                baseZoomScale,
                isDragging,
                isAnimatingPageTurn,
                inputController.ActivePointerCount,
                lastPointerPos,
                inputController.DragStartPos,
                pageTurnAnimCurlAmount,
                pageTurnCurlFromRight,
                pageTurnCurlingNode,
                layoutSnapshot,
                allNodesSnapshot,
                inputController.IsPageTurnGesture,
                pageTurnCurlTilt);

            pageRenderer.Draw(renderContext);
        }
    }

    /// <summary>
    /// 与翻页完成互斥，避免按切换中的布局或缩放淘汰仍需显示的位图。
    /// </summary>
    private void UpdateImageWindow(CanvasDevice device)
    {
        lock (pageTurnLock)
        {
            if (isPreparingPages) return;
            var size = viewSize;
            if (size == Vector2.Zero) return;

            var center = size / 2;
            var transform = Matrix3x2.CreateTranslation(-state.CameraPos) *
                            Matrix3x2.CreateScale(state.Zoom) *
                            Matrix3x2.CreateTranslation(center);

            if (!Matrix3x2.Invert(transform, out var inverseTransform)) return;
            var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
            var bottomRight = Vector2.Transform(size, inverseTransform);
            var viewportRect = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

            List<RenderNode> layoutSnapshot;
            lock (state.LayoutNodes)
            {
                layoutSnapshot = state.LayoutNodes.ToList();
            }

            List<RenderNode> allNodesSnapshot;
            lock (allNodes)
            {
                allNodesSnapshot = allNodes.ToList();
            }

            imageWindowLoadController.UpdateWindow(
                layoutSnapshot,
                allNodesSnapshot,
                viewportRect,
                preloadRange,
                device,
                TryEnqueueBitmapLoad);
        }
    }
}
