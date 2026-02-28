using System;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.Rendering;

/// <summary>
/// 基于 Win2D 的页面渲染器实现。
/// </summary>
internal sealed class Win2DPageRenderer : IPageRenderer
{
    /// <inheritdoc/>
    public void Draw(in PageRenderContext context)
    {
        var drawingSession = context.DrawingSession;

        bool isCurling = (context.Mode == ReadingMode.SpreadLtr || context.Mode == ReadingMode.SpreadRtl)
                         && Math.Abs(context.Zoom - context.BaseZoomScale) <= 0.001f
                         && ((context.IsDragging && context.ActivePointerCount == 1) || context.IsAnimatingPageTurn);

        RenderNode? curlingNode = null;
        bool curlFromRight = false;
        float curlAmount = 0;

        if (isCurling)
        {
            if (context.IsAnimatingPageTurn)
            {
                curlFromRight = context.PageTurnCurlFromRight;
                curlAmount = context.PageTurnAnimCurlAmount;
                curlingNode = context.PageTurnCurlingNode;
            }
            else
            {
                float dragDeltaX = context.LastPointerPos.X - context.DragStartPos.X;
                if (dragDeltaX < -10)
                {
                    curlFromRight = true;
                    curlAmount = -dragDeltaX / context.Zoom;
                    curlingNode = context.LayoutNodes.OrderByDescending(n => n.Bounds.X).FirstOrDefault();
                }
                else if (dragDeltaX > 10)
                {
                    curlFromRight = false;
                    curlAmount = dragDeltaX / context.Zoom;
                    curlingNode = context.LayoutNodes.OrderBy(n => n.Bounds.X).FirstOrDefault();
                }
            }
        }

        foreach (var node in context.LayoutNodes)
        {
            if (node == curlingNode)
            {
                continue;
            }

            if (IsIntersecting(context.ViewportRect, node.Bounds))
            {
                DrawNodeNormal(drawingSession, node);
            }
        }

        if (curlingNode == null)
        {
            return;
        }

        RenderNode? nodeUnderneath = null;
        RenderNode? nodeBack = null;

        int nextIndex = curlingNode.PageIndex + (curlFromRight ? 2 : -2);
        int nextBackIndex = curlingNode.PageIndex + (curlFromRight ? 1 : -1);

        if (nextIndex >= 0 && nextIndex < context.AllNodes.Count)
        {
            nodeUnderneath = context.AllNodes[nextIndex];
        }

        if (nextBackIndex >= 0 && nextBackIndex < context.AllNodes.Count)
        {
            nodeBack = context.AllNodes[nextBackIndex];
        }

        if (nodeUnderneath != null)
        {
            bool drewUnder = false;

            nodeUnderneath.UseBitmap(bitmap =>
            {
                drawingSession.DrawImage(bitmap, nodeUnderneath.Bounds);
                drewUnder = true;
            });

            if (!drewUnder)
            {
                drawingSession.DrawRectangle(curlingNode.Bounds, Windows.UI.Color.FromArgb(255, 50, 50, 50));
            }
        }

        DrawCurledPage(drawingSession, curlingNode, nodeBack, curlAmount, curlFromRight);
    }

    /// <summary>
    /// 常规页面绘制：优先绘制位图，缺失时回退占位框。
    /// </summary>
    /// <param name="drawingSession">当前绘制会话。</param>
    /// <param name="node">待绘制节点。</param>
    private static void DrawNodeNormal(CanvasDrawingSession drawingSession, RenderNode node)
    {
        bool drew = false;
        node.UseBitmap(bitmap =>
        {
            try
            {
                drawingSession.DrawImage(bitmap, node.Bounds);
                drew = true;
            }
            catch
            {
                // 绘制异常按占位符降级，不中断整帧渲染。
            }
        });

        if (drew)
        {
            return;
        }

        drawingSession.DrawRectangle(node.Bounds, Windows.UI.Color.FromArgb(255, 100, 100, 100));
        using var format = new CanvasTextFormat
        {
            FontSize = 24,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };

        drawingSession.DrawText($"{node.PageIndex + 1}", node.Bounds, Windows.UI.Color.FromArgb(255, 200, 200, 200), format);
    }

    /// <summary>
    /// 绘制卷页效果。
    /// </summary>
    /// <param name="drawingSession">当前绘制会话。</param>
    /// <param name="node">卷起页面节点。</param>
    /// <param name="nodeUnderneath">背面页面节点。</param>
    /// <param name="curlAmount">卷曲量。</param>
    /// <param name="curlFromRight">是否从右侧卷起。</param>
    private static void DrawCurledPage(CanvasDrawingSession drawingSession, RenderNode node, RenderNode? nodeUnderneath, float curlAmount, bool curlFromRight)
    {
        if (curlAmount <= 0)
        {
            DrawNodeNormal(drawingSession, node);
            return;
        }

        float width = (float)node.Bounds.Width;
        float height = (float)node.Bounds.Height;
        float offsetX = (float)node.Bounds.X;
        float offsetY = (float)node.Bounds.Y;

        float radius = (float)Math.Min(40.0, curlAmount / Math.PI);
        if (radius < 1.0f)
        {
            radius = 1.0f;
        }

        float curlLength = (float)(curlAmount / 2.0 + Math.PI * radius / 2.0);

        CanvasBitmap? frontBitmap = null;
        CanvasBitmap? backBitmap = null;

        node.UseBitmap(bitmap => frontBitmap = bitmap);
        nodeUnderneath?.UseBitmap(bitmap => backBitmap = bitmap);

        if (frontBitmap == null)
        {
            DrawNodeNormal(drawingSession, node);
            return;
        }

        using var spriteBatch = drawingSession.CreateSpriteBatch(CanvasSpriteSortMode.None, CanvasImageInterpolation.Linear, CanvasSpriteOptions.None);

        const float stripWidth = 2.0f;
        int stripCount = (int)Math.Ceiling(width / stripWidth);

        Action<int> drawStrip = i =>
        {
            float x = i * stripWidth;
            float currentStripWidth = Math.Min(stripWidth, width - x);
            if (currentStripWidth <= 0)
            {
                return;
            }

            float transformedX;
            float scaleX;
            float shade;

            if (curlFromRight)
            {
                float curlX = width - curlLength;
                float d = x - curlX;

                if (d <= 0)
                {
                    transformedX = x;
                    scaleX = 1;
                    shade = 1.0f;
                }
                else if (d < Math.PI * radius)
                {
                    float alpha = d / radius;
                    transformedX = curlX + radius * (float)Math.Sin(alpha);
                    scaleX = (float)Math.Cos(alpha);
                    shade = 1.0f - 0.3f * (float)Math.Sin(alpha);
                }
                else
                {
                    transformedX = curlX - (d - (float)Math.PI * radius);
                    scaleX = -1;
                    shade = 0.6f;
                }
            }
            else
            {
                float curlX = curlLength;
                float d = curlX - x;

                if (d <= 0)
                {
                    transformedX = x;
                    scaleX = 1;
                    shade = 1.0f;
                }
                else if (d < Math.PI * radius)
                {
                    float alpha = d / radius;
                    transformedX = curlX - radius * (float)Math.Sin(alpha);
                    scaleX = (float)Math.Cos(alpha);
                    shade = 1.0f - 0.3f * (float)Math.Sin(alpha);
                }
                else
                {
                    transformedX = curlX + (d - (float)Math.PI * radius);
                    scaleX = -1;
                    shade = 0.6f;
                }
            }

            if (Math.Abs(scaleX) < 0.001f)
            {
                return;
            }

            bool isBack = scaleX < 0;
            CanvasBitmap? currentBitmap = isBack && backBitmap != null ? backBitmap : frontBitmap;
            if (currentBitmap == null)
            {
                return;
            }

            float drawScaleX = scaleX;
            float destX = transformedX;
            float sourceX = x;

            if (isBack && backBitmap != null)
            {
                drawScaleX = -scaleX;
                destX = transformedX - currentStripWidth * drawScaleX;
                sourceX = width - x - currentStripWidth;
            }

            Rect sourceRect = new(
                (sourceX / width) * currentBitmap.Size.Width,
                0,
                (currentStripWidth / width) * currentBitmap.Size.Width,
                currentBitmap.Size.Height);

            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                return;
            }

            float totalScaleX = (currentStripWidth * drawScaleX) / (float)sourceRect.Width;
            float totalScaleY = height / (float)sourceRect.Height;

            Matrix3x2 finalTransform = Matrix3x2.CreateScale(totalScaleX, totalScaleY) *
                                       Matrix3x2.CreateTranslation(offsetX + destX, offsetY);

            Vector4 tint = new(shade, shade, shade, 1.0f);
            spriteBatch.DrawFromSpriteSheet(currentBitmap, finalTransform, sourceRect, tint);
        };

        if (curlFromRight)
        {
            for (int i = 0; i < stripCount; i++)
            {
                drawStrip(i);
            }
        }
        else
        {
            for (int i = stripCount - 1; i >= 0; i--)
            {
                drawStrip(i);
            }
        }
    }

    /// <summary>
    /// 矩形相交判定。
    /// </summary>
    /// <param name="a">矩形 A。</param>
    /// <param name="b">矩形 B。</param>
    /// <returns>相交返回 <c>true</c>。</returns>
    private static bool IsIntersecting(Rect a, Rect b)
    {
        return a.X < b.X + b.Width &&
               a.X + a.Width > b.X &&
               a.Y < b.Y + b.Height &&
               a.Y + a.Height > b.Y;
    }
}
