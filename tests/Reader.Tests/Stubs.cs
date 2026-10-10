// Drawing adapters record production renderer calls; they do not simulate the GPU.
using System.Numerics;
using Windows.Foundation;
namespace Windows.Foundation
{
    public struct Rect(double x, double y, double width, double height)
    {
        public double X = x, Y = y, Width = width, Height = height;
        public override string ToString() => $"({X},{Y},{Width},{Height})";
    }
    public struct Size(double width, double height) { public double Width = width, Height = height; }
}
namespace Windows.UI
{
    public struct Color
    {
        public byte A, R, G, B;
        public static Color FromArgb(byte a, byte r, byte g, byte b) => new() { A = a, R = r, G = g, B = b };
    }
}
namespace Microsoft.Graphics.Canvas
{
    public enum CanvasSpriteSortMode { None }
    public enum CanvasImageInterpolation { Linear }
    public enum CanvasSpriteOptions { None }
    public enum CanvasAntialiasing { Antialiased, Aliased }
    public sealed class CanvasDevice { }
    public sealed class CanvasBitmap(int pageIndex, Size size) : IDisposable
    {
        public int PageIndex { get; } = pageIndex;
        public Size Size { get; } = size;
        public bool IsDisposed { get; private set; }
        public Action? Disposing { get; set; }
        public void Dispose()
        {
            Disposing?.Invoke();
            IsDisposed = true;
        }
    }
    public sealed class CanvasDrawingSession
    {
        public Matrix3x2 Transform { get; set; }
        public CanvasAntialiasing Antialiasing { get; set; }
        public List<(int Page, Vector2[] Polygon, Matrix3x2 Texture)> Polygons { get; } = [];
        public List<(Vector2[] Polygon, Vector2 Start, Vector2 End)> DiagonalShadows { get; } = [];
        public void FillGeometry(Geometry.CanvasGeometry geometry, Brushes.CanvasImageBrush brush) =>
            Polygons.Add((brush.Bitmap.PageIndex, geometry.Points, brush.Transform));
        public void FillGeometry(Geometry.CanvasGeometry geometry, Brushes.CanvasLinearGradientBrush brush) =>
            DiagonalShadows.Add((geometry.Points, brush.StartPoint, brush.EndPoint));
        public void FillGeometry(Geometry.CanvasGeometry geometry, Windows.UI.Color color) { }
        public List<(int Page, Rect Bounds)> Images { get; } = [];
        public List<(int Page, Matrix3x2 Transform, Rect Source)> Sprites { get; } = [];
        public List<Vector4> Tints { get; } = [];
        public List<(Rect Bounds, Vector2 Start, Vector2 End, Brushes.CanvasGradientStop[] Stops)> Shadows { get; } = [];
        public void FillRectangle(Rect rect, Brushes.CanvasLinearGradientBrush brush) =>
            Shadows.Add((rect, brush.StartPoint, brush.EndPoint, brush.Stops));
        public void DrawImage(CanvasBitmap bitmap, Rect rect) => Images.Add((bitmap.PageIndex, rect));
        public void DrawRectangle(Rect rect, Windows.UI.Color color) { }
        public void DrawText(string text, Rect rect, Windows.UI.Color color, Text.CanvasTextFormat format) { }
        public CanvasSpriteBatch CreateSpriteBatch(CanvasSpriteSortMode sort, CanvasImageInterpolation interp, CanvasSpriteOptions options) => new(this);
    }
    public sealed class CanvasSpriteBatch(CanvasDrawingSession session) : IDisposable
    {
        public void DrawFromSpriteSheet(CanvasBitmap bitmap, Matrix3x2 transform, Rect rect, Vector4 tint)
        {
            session.Sprites.Add((bitmap.PageIndex, transform, rect));
            session.Tints.Add(tint);
        }
        public void Dispose() { }
    }
}
namespace Microsoft.Graphics.Canvas.Brushes
{
    public sealed class CanvasImageBrush(Microsoft.Graphics.Canvas.CanvasDrawingSession session,
        Microsoft.Graphics.Canvas.CanvasBitmap bitmap) : IDisposable
    {
        public Microsoft.Graphics.Canvas.CanvasBitmap Bitmap { get; } = bitmap;
        public Matrix3x2 Transform { get; set; }
        public void Dispose() { }
    }
    public struct CanvasGradientStop { public float Position; public Windows.UI.Color Color; }
    public sealed class CanvasLinearGradientBrush(Microsoft.Graphics.Canvas.CanvasDrawingSession session, CanvasGradientStop[] stops) : IDisposable
    {
        public CanvasGradientStop[] Stops { get; } = stops;
        public Vector2 StartPoint { get; set; }
        public Vector2 EndPoint { get; set; }
        public void Dispose() { }
    }
}
namespace Microsoft.Graphics.Canvas.Geometry
{
    public sealed class CanvasGeometry(Vector2[] points) : IDisposable
    {
        public Vector2[] Points { get; } = points;
        public static CanvasGeometry CreatePolygon(Microsoft.Graphics.Canvas.CanvasDrawingSession session,
            Vector2[] points) => new(points);
        public void Dispose() { }
    }
}
namespace Microsoft.Graphics.Canvas.Text
{
    public enum CanvasHorizontalAlignment { Center }
    public enum CanvasVerticalAlignment { Center }
    public sealed class CanvasTextFormat : IDisposable
    {
        public float FontSize { get; set; }
        public CanvasHorizontalAlignment HorizontalAlignment { get; set; }
        public CanvasVerticalAlignment VerticalAlignment { get; set; }
        public void Dispose() { }
    }
}
namespace FluentIcons.Common { public enum Icon { DualScreenVerticalScroll, DocumentHeader, BookOpen } }
namespace ShadowViewer.Controls.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MenuFlyoutItemIconAttribute : Attribute { public FluentIcons.Common.Icon Icon { get; set; } }
}
namespace Serilog { public static class Log { public static void Debug(string message, params object?[] values) { } } }
