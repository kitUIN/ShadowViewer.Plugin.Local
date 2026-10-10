// Only adapts WinUI event data so the production input handlers run in the harness.
// Native event routing, capture and popup rendering still require a Windows UI check.
using System.Numerics;

namespace Windows.System
{
    [Flags]
    public enum VirtualKeyModifiers { None = 0, Control = 1 }
}

namespace Microsoft.UI.Input
{
    public enum PointerDeviceType { Mouse, Touch }
}

namespace Microsoft.UI.Xaml.Input
{
    public sealed class TestCanvas
    {
        public void CapturePointer(TestPointer pointer) { }
    }

    public sealed class TestPointer(uint id)
    {
        public uint PointerId { get; } = id;
        public Microsoft.UI.Input.PointerDeviceType PointerDeviceType => Microsoft.UI.Input.PointerDeviceType.Touch;
    }

    public readonly struct TestPosition(Vector2 position)
    {
        public Vector2 ToVector2() => position;
    }

    public sealed class TestPointProperties(int delta)
    {
        public bool IsLeftButtonPressed => true;
        public int MouseWheelDelta => delta;
    }

    public sealed class TestPoint(uint id, Vector2 position, int delta)
    {
        public uint PointerId { get; } = id;
        public TestPosition Position { get; } = new(position);
        public TestPointProperties Properties { get; } = new(delta);
    }

    public sealed class PointerRoutedEventArgs(uint id, Vector2 position, int delta = 0)
    {
        public TestPointer Pointer { get; } = new(id);
        public Windows.System.VirtualKeyModifiers KeyModifiers { get; set; }
        public bool Handled { get; set; }
        public TestPoint GetCurrentPoint(TestCanvas? canvas) => new(id, position, delta);
    }
}
