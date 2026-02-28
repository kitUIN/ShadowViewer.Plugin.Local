using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.Rendering;

/// <summary>
/// 页面渲染器抽象，负责将节点集合绘制到 Win2D 画布。
/// </summary>
internal interface IPageRenderer
{
    /// <summary>
    /// 按给定上下文执行页面绘制。
    /// </summary>
    /// <param name="context">渲染上下文。</param>
    /// <returns>无返回值。</returns>
    void Draw(in PageRenderContext context);
}

/// <summary>
/// 页面渲染上下文。
/// </summary>
internal readonly record struct PageRenderContext(
    CanvasDrawingSession DrawingSession,
    Rect ViewportRect,
    ReadingMode Mode,
    float Zoom,
    float BaseZoomScale,
    bool IsDragging,
    bool IsAnimatingPageTurn,
    int ActivePointerCount,
    Vector2 LastPointerPos,
    Vector2 DragStartPos,
    float PageTurnAnimCurlAmount,
    bool PageTurnCurlFromRight,
    RenderNode? PageTurnCurlingNode,
    IReadOnlyList<RenderNode> LayoutNodes,
    IReadOnlyList<RenderNode> AllNodes);
