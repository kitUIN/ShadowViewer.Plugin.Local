using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using ShadowViewer.Plugin.Local.Readers.Internal;
using Windows.Foundation;
using Windows.UI;

namespace ShadowViewer.Plugin.Local.Readers.Rendering;

internal sealed partial class Win2DPageRenderer
{
    private static void DrawDiagonalBitmap(CanvasDrawingSession session, Rect bounds, CanvasBitmap front,
        CanvasBitmap? back, Rect backBounds, bool fromRight, PageCurlGeometry curl, Rect frontSource, Rect backSource)
    {
        using var frontBrush = new CanvasImageBrush(session, front);
        using var backBrush = new CanvasImageBrush(session, back ?? front);
        float width = (float)bounds.Width;
        float height = (float)bounds.Height;
        float spine = (float)bounds.X + (fromRight ? 0 : width);
        var backFit = Matrix3x2.CreateTranslation(-spine, -(float)bounds.Y) *
            Matrix3x2.CreateScale(1 + ((float)backBounds.Width / width - 1) * curl.Progress,
                1 + ((float)backBounds.Height / height - 1) * curl.Progress) *
            Matrix3x2.CreateTranslation(spine, (float)(bounds.Y + (backBounds.Y - bounds.Y) * curl.Progress));

        // 相邻条带共用端点并关闭内部边缘抗锯齿，防止纸面出现透明细缝。
        var antialiasing = session.Antialiasing;
        try
        {
            session.Antialiasing = CanvasAntialiasing.Aliased;
            foreach (var strip in curl.GetStrips())
            {
                if (strip.Polygon.Length < 3) continue;
                bool useBack = strip.IsBack && back != null;
                var bitmap = useBack ? back! : front;
                var brush = useBack ? backBrush : frontBrush;
                var fold = strip.Transform * (useBack ? backFit : Matrix3x2.Identity);
                // 背面纹理先镜像到纸张坐标，再随纸面一起卷曲，文字朝向保持正确。
                var source = useBack ? backSource : frontSource;
                var texture = Matrix3x2.CreateTranslation(-(float)source.X, -(float)source.Y) *
                    Matrix3x2.CreateScale(width / (float)source.Width, height / (float)source.Height);
                if (useBack) texture *= new Matrix3x2(-1, 0, 0, 1, width, 0);
                brush.Transform = texture * Matrix3x2.CreateTranslation((float)bounds.X, (float)bounds.Y) * fold;
                using var geometry = CanvasGeometry.CreatePolygon(session,
                    strip.Polygon.Select(p => Vector2.Transform(p, fold)).ToArray());
                session.FillGeometry(geometry, brush);
                byte shadow = (byte)((1 - strip.Shade) * 255);
                if (shadow > 0) session.FillGeometry(geometry, Color.FromArgb(shadow, 0, 0, 0));
            }
        }
        finally
        {
            session.Antialiasing = antialiasing;
        }
    }

    private static void DrawDiagonalShadow(CanvasDrawingSession session, PageCurlGeometry curl, Rect page,
        IEnumerable<Rect> receivers)
    {
        float lift = MathF.Sin(MathF.PI * curl.Progress);
        float width = Math.Min((float)page.Width * 0.12f, 12 + curl.Radius) * lift;
        if (width <= 0) return;
        float edge = curl.Crease + curl.Radius;
        using var brush = new CanvasLinearGradientBrush(session,
        [
            new CanvasGradientStop { Position = 0, Color = Color.FromArgb(0, 0, 0, 0) },
            new CanvasGradientStop { Position = 0.5f, Color = Color.FromArgb((byte)(72 * lift), 0, 0, 0) },
            new CanvasGradientStop { Position = 1, Color = Color.FromArgb(0, 0, 0, 0) }
        ])
        {
            StartPoint = curl.Normal * (edge - width),
            EndPoint = curl.Normal * (edge + width)
        };
        foreach (var receiver in receivers)
        {
            var polygon = PageCurlGeometry.ClipBand(PageCurlGeometry.RectangleCorners(receiver),
                curl.Normal, edge - width, edge + width);
            if (polygon.Length < 3) continue;
            using var geometry = CanvasGeometry.CreatePolygon(session, polygon);
            session.FillGeometry(geometry, brush);
        }
    }
}
