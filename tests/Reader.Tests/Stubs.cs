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
    public sealed class CanvasBitmap(int pageIndex, Size size) : IDisposable
    {
        public int PageIndex { get; } = pageIndex;
        public Size Size { get; } = size;
        public void Dispose() { }
    }
    public sealed class CanvasDrawingSession
    {
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
    public struct CanvasGradientStop { public float Position; public Windows.UI.Color Color; }
    public sealed class CanvasLinearGradientBrush(Microsoft.Graphics.Canvas.CanvasDrawingSession session, CanvasGradientStop[] stops) : IDisposable
    {
        public CanvasGradientStop[] Stops { get; } = stops;
        public Vector2 StartPoint { get; set; }
        public Vector2 EndPoint { get; set; }
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
namespace ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies { public interface IImageSourceStrategy { } }
namespace Serilog { public static class Log { public static void Debug(string message, params object?[] values) { } } }
