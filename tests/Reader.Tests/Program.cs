using System.Numerics;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using ShadowViewer.Plugin.Local.Readers.Internal;
using ShadowViewer.Plugin.Local.Readers.Rendering;
using Windows.Foundation;

var service = new PageTurnService();
var layout = new ReaderLayoutService();
var renderer = new Win2DPageRenderer();
int passed = 0, failed = 0;
void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
    if (ok) passed++; else failed++;
}
(EngineState State, List<RenderNode> Nodes) Scene(ReadingMode mode, int index, int count = 9)
{
    var nodes = Enumerable.Range(0, count).Select(i => new RenderNode
    {
        PageIndex = i,
        Ctx = new ImageLoadingContext { Size = new Size(1000, 1400) },
        Source = i,
        Bounds = new Rect(0, 0, 1000, 1400),
        IsSizeLoaded = true,
        Bitmap = new CanvasBitmap(i, new Size(1000, 1400))
    }).ToList();
    var state = new EngineState { CurrentMode = mode };
    layout.UpdateActiveLayout(state, nodes, index, 0, false, new Vector2(2000, 1400), new ReaderLayoutCacheState());
    return (state, nodes);
}
PageTurnPlan Plan(EngineState state, int current, Vector2 delta, float velocity = 0, int count = 9)
{
    if (!service.TryCreatePlan(new PageTurnRequest(delta, velocity, state.Zoom, current, count, state.CurrentMode, state.LayoutNodes), out var result))
        throw new Exception("Expected a page-turn/rebound plan");
    return result;
}
CanvasDrawingSession DrawPlan(EngineState state, List<RenderNode> nodes, PageTurnPlan plan, float? curl = null)
{
    var drawing = new CanvasDrawingSession();
    renderer.Draw(new PageRenderContext(drawing, new Rect(-1100, -800, 2200, 1600), state.CurrentMode,
        1, 1, false, true, 0, Vector2.Zero, Vector2.Zero, curl ?? plan.CurrentCurl,
        plan.CurlFromRight, plan.CurlingNode, state.LayoutNodes, nodes));
    return drawing;
}
foreach (var sample in new[]
{
    (Mode: ReadingMode.SpreadLtr, Current: 3, Delta: -100f, Expected: 5, Name: "LTR swipe left advances"),
    (Mode: ReadingMode.SpreadLtr, Current: 3, Delta: 100f, Expected: 1, Name: "LTR swipe right goes back"),
    (Mode: ReadingMode.SpreadRtl, Current: 4, Delta: 100f, Expected: 6, Name: "RTL swipe right advances"),
    (Mode: ReadingMode.SpreadRtl, Current: 4, Delta: -100f, Expected: 2, Name: "RTL swipe left goes back")
})
{
    var (state, nodes) = Scene(sample.Mode, sample.Current);
    var plan = Plan(state, sample.Current, new Vector2(sample.Delta, 0));
    Check(sample.Name, plan.TargetPageIndex == sample.Expected,
        $"expected index {sample.Expected}, actual {plan.TargetPageIndex}, curling index {plan.CurlingNode!.PageIndex}");
}
{
    var (state, nodes) = Scene(ReadingMode.SpreadRtl, 4);
    var plan = Plan(state, 4, new Vector2(100, 0));
    var drawing = DrawPlan(state, nodes, plan, 800);
    var underlying = drawing.Images.Single(i => !state.LayoutNodes.Any(n => n.PageIndex == i.Page));
    Check("RTL forward underlying page", underlying.Page == 6, $"expected index 6, actual {underlying.Page}");
    Check("RTL forward reverse page", drawing.Sprites.Any(s => s.Page == 5),
        $"expected back index 5, actual sprite indexes {string.Join(',', drawing.Sprites.Select(s => s.Page).Distinct())}");
}
{
    var (state, nodes) = Scene(ReadingMode.SpreadLtr, 3);
    var plan = Plan(state, 3, new Vector2(-100, 0));
    var drawing = DrawPlan(state, nodes, plan);
    var underlying = drawing.Images.Single(i => i.Page == 6);
    var front = plan.CurlingNode!.Bounds;
    Check("LTR underlying page aligned with curling page",
        underlying.Bounds.X == front.X && underlying.Bounds.Y == front.Y &&
        underlying.Bounds.Width == front.Width && underlying.Bounds.Height == front.Height,
        $"expected {front}, actual {underlying.Bounds}");
    var endDrawing = DrawPlan(state, nodes, plan, plan.TargetCurl);
    var frontStrips = endDrawing.Sprites.Where(s => s.Page == plan.CurlingNode.PageIndex && s.Transform.M11 > 0).ToList();
    Check("Completed curl has no positive-scale front strips", frontStrips.Count == 0,
        $"target curl {plan.TargetCurl}, page width {front.Width}, remaining front strips {frontStrips.Count}");
}
{
    var (state, nodes) = Scene(ReadingMode.SpreadLtr, 3);
    var small = Plan(state, 3, new Vector2(-30, 0));
    Check("Short slow drag rebounds", small.TargetPageIndex == 3 && small.TargetCurl == 0 && small.AnimVelocity < 0,
        $"target index {small.TargetPageIndex}, target curl {small.TargetCurl}, velocity {small.AnimVelocity}");
    var vertical = Plan(state, 3, new Vector2(-100, 200));
    Check("Vertical-dominant drag does not navigate", vertical.TargetPageIndex == 3 && vertical.TargetCurl == 0,
        $"target index {vertical.TargetPageIndex}");
    bool tiny = service.TryCreatePlan(new PageTurnRequest(new Vector2(-5, 0), 0, 1, 3, 9, state.CurrentMode, state.LayoutNodes), out _);
    Check("Tiny motion ignored", !tiny, $"created plan {tiny}");
    bool zeroZoom = service.TryCreatePlan(new PageTurnRequest(new Vector2(-100, 0), 0, 0, 3, 9, state.CurrentMode, state.LayoutNodes), out _);
    Check("Invalid zoom ignored", !zeroZoom, $"created plan {zeroZoom}");
    foreach (int hz in new[] { 30, 60, 120 })
    {
        foreach (var direction in new[] { "turn", "rebound" })
        {
            var plan = direction == "turn" ? Plan(state, 3, new Vector2(-100, 0)) : small;
            float amount = plan.CurrentCurl, velocity = plan.AnimVelocity;
            bool finished = false;
            int frame = 0;
            for (; frame < hz && !finished; frame++)
            {
                var step = service.StepAnimation(amount, plan.TargetCurl, velocity, 1f / hz);
                amount = step.CurlAmount;
                velocity = step.Velocity;
                finished = step.IsFinished;
            }
            Check($"{direction} converges at {hz}Hz", finished && amount == plan.TargetCurl && velocity == 0,
                $"frames {frame}, seconds {(float)frame/hz:F3}, curl {amount}, velocity {velocity}");
        }
    }
}
foreach (var boundary in new[] { (Index: 0, Delta: 100f), (Index: 8, Delta: -100f) })
{
    var (state, nodes) = Scene(ReadingMode.SpreadLtr, boundary.Index);
    var plan = Plan(state, boundary.Index, new Vector2(boundary.Delta, 0));
    Check($"Boundary index {boundary.Index} rebounds", plan.TargetPageIndex == boundary.Index && plan.TargetCurl == 0,
        $"target index {plan.TargetPageIndex}, target curl {plan.TargetCurl}");
}
{
    var (state, nodes) = Scene(ReadingMode.SpreadLtr, 3);
    double time = 0;
    var input = new ReaderInputController(() => time);
    input.TryHandlePointerPressed(1, new Vector2(1000, 500), false, 0, 1, false);
    time = 0.016;
    input.HandlePointerMoved(1, new Vector2(970, 500));
    var delta = input.ConsumeFrameDelta();
    var frame = new ReaderFrameOrchestrator();
    frame.Step(state, delta, 0.016f, 1, new Vector2(2000, 1400), true, false, false, 0, 0, 0, Vector2.Zero, service);
    var release = input.HandlePointerLost(1);
    var plan = Plan(state, 3, release.PointerPosition - input.DragStartPos, release.Velocity.X / state.Zoom);
    Check("Fast 30px horizontal flick advances", plan.TargetPageIndex == 5,
        $"30px/16ms (>500px/s), gesture velocity {release.Velocity.X}, camera velocity {state.Velocity.X}, actual target index {plan.TargetPageIndex}");
}
{
    var input = new ReaderInputController();
    input.TryHandlePointerPressed(1, new Vector2(1000, 500), true, 300, 1, true);
    input.HandlePointerMoved(1, new Vector2(980, 500));
    var release = input.HandlePointerLost(1);
    Check("Interrupt preserves curl continuity", release.PointerPosition.X - input.DragStartPos.X == -320,
        $"drag delta {release.PointerPosition.X - input.DragStartPos.X}");
    Check("Duplicate pointer loss ignored", !input.HandlePointerLost(1).IsTrackedPointer, "second loss for same id");
}

foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    var (state, nodes) = Scene(mode, 0);
    double expectedX = mode == ReadingMode.SpreadLtr ? 0 : -1000;
    Check($"{mode} cover side", state.LayoutNodes[0].Bounds.X == expectedX,
        $"expected X {expectedX}, actual {state.LayoutNodes[0].Bounds.X}");
    var delta = new Vector2(mode == ReadingMode.SpreadLtr ? -100 : 100, 0);
    var plan = Plan(state, 0, delta);
    var drawing = DrawPlan(state, nodes, plan, 800);
    Check($"{mode} cover reveals first spread", plan.TargetPageIndex == 2 &&
        drawing.Images.Any(i => i.Page == 2) && drawing.Sprites.Any(s => s.Page == 1),
        $"target {plan.TargetPageIndex}, underlying/back 2/1");
}
{
    var (state, nodes) = Scene(ReadingMode.SpreadLtr, 3);
    service.TryCreatePlan(new PageTurnRequest(new Vector2(-200, 0), -1000, 1, 3, 9,
        state.CurrentMode, state.LayoutNodes, isCanceled: true), out var plan);
    Check("Canceled gesture rebounds", plan.TargetPageIndex == 3 && plan.TargetCurl == 0,
        $"target {plan.TargetPageIndex}, curl {plan.TargetCurl}");
    bool empty = service.TryCreatePlan(new PageTurnRequest(new Vector2(-100, 0), 0, 1, 0, 0,
        state.CurrentMode, Array.Empty<RenderNode>()), out _);
    Check("Empty chapter ignores page turns", !empty, $"created plan {empty}");
}
{
    double time = 0;
    var input = new ReaderInputController(() => time);
    input.TryHandlePointerPressed(1, new Vector2(1000, 500), false, 0, 1, false);
    time = 0.016;
    input.HandlePointerMoved(1, new Vector2(970, 500));
    time = 0.3;
    Check("Holding still clears flick velocity", input.HandlePointerLost(1).Velocity == Vector2.Zero,
        "release after 284ms stationary");
    input.TryHandlePointerPressed(1, new Vector2(1000, 500), false, 0, 1, false);
    input.TryHandlePointerPressed(2, new Vector2(1500, 500), false, 0, 1, false);
    input.HandlePointerMoved(1, new Vector2(900, 500));
    input.HandlePointerMoved(2, new Vector2(1400, 500));
    input.HandlePointerLost(2);
    Check("Two-finger gesture cannot turn a page", !input.HandlePointerLost(1).IsPageTurnGesture,
        "two-finger translation followed by sequential release");
    input.TryHandlePointerPressed(1, new Vector2(1000, 500), false, 0, 1, false);
    input.HandlePointerMoved(1, new Vector2(800, 500));
    input.Reset();
    var pending = input.ConsumeFrameDelta();
    Check("Reset clears pointers and pending input", input.ActivePointerCount == 0 &&
        pending.PanDelta == Vector2.Zero && pending.ZoomDelta == 1 && !input.HandlePointerLost(1).IsTrackedPointer,
        "chapter change discards old pointer events");
}

foreach (var mode in new[] { ReadingMode.SinglePage, ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    foreach (int current in new[] { 0, 1, 3, 8 })
    {
        var (state, nodes) = Scene(mode, current);
        foreach (var node in nodes) node.Bounds = new Rect(123, 456, 17, 23);
        layout.UpdateActiveLayout(state, nodes, current, 0, false, new Vector2(2000, 1400), new ReaderLayoutCacheState());
        int pairStart = current == 0 ? 0 : (current - 1) / 2 * 2 + 1;
        var nearby = mode == ReadingMode.SinglePage
            ? Enumerable.Range(Math.Max(0, current - 1), Math.Min(8, current + 1) - Math.Max(0, current - 1) + 1)
            : Enumerable.Range(Math.Max(0, pairStart - 2), Math.Min(8, pairStart + (pairStart == 0 ? 2 : 3)) - Math.Max(0, pairStart - 2) + 1);
        bool matches = true;
        foreach (int index in nearby)
        {
            var (expected, _) = Scene(mode, index);
            var bounds = expected.LayoutNodes.Single(n => n.PageIndex == index).Bounds;
            matches &= nodes[index].Bounds.Equals(bounds);
        }
        Check($"{mode} precomputes neighbors at {current}", matches &&
            state.LayoutNodes.All(n => mode == ReadingMode.SinglePage ? n.PageIndex == current :
                current == 0 ? n.PageIndex == 0 : n.PageIndex == pairStart || n.PageIndex == pairStart + 1),
            "adjacent bounds match their active layout; only current pages are visible");
    }
}

foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    foreach (int sign in new[] { -1, 1 })
    {
        var (state, nodes) = Scene(mode, 3);
        foreach (var node in nodes) node.Ctx.Size = new Size(600 + node.PageIndex * 73, 900 + node.PageIndex * 91);
        layout.UpdateActiveLayout(state, nodes, 3, 0, false, new Vector2(2000, 1400), new ReaderLayoutCacheState());
        var plan = Plan(state, 3, new Vector2(sign * 100, 0));
        int direction = PageTurnService.GetPageDirection(plan.CurlFromRight, mode);
        var underneath = nodes[plan.CurlingNode!.PageIndex + direction * 2];
        var back = nodes[plan.CurlingNode.PageIndex + direction];
        var drawing = DrawPlan(state, nodes, plan, plan.TargetCurl);
        Check($"{mode} {sign} underlying page keeps target bounds",
            drawing.Images.Single(i => i.Page == underneath.PageIndex).Bounds.Equals(underneath.Bounds),
            $"different page sizes: expected {underneath.Bounds}");
        var strips = drawing.Sprites.Where(s => s.Page == back.PageIndex).ToList();
        float minX = strips.Min(s => s.Transform.M31);
        float maxX = strips.Max(s => s.Transform.M31 + (float)s.Source.Width * s.Transform.M11);
        Check($"{mode} {sign} completed reverse aligns with target layout",
            Math.Abs(minX - back.Bounds.X) < 0.01 && Math.Abs(maxX - back.Bounds.X - back.Bounds.Width) < 0.01 &&
            strips.All(s => Math.Abs(s.Transform.M32 - back.Bounds.Y) < 0.01 &&
                Math.Abs(s.Source.Height * s.Transform.M22 - back.Bounds.Height) < 0.01),
            $"expected {back.Bounds}, sprite X extent {minX:F2}..{maxX:F2}");
    }
}

Console.WriteLine($"TOTAL {passed + failed}: PASS {passed}, FAIL {failed}");
Console.WriteLine("Uses production source links; Windows/Win2D adapters record draw calls only, no native GPU or UI validation.");
Environment.ExitCode = failed == 0 ? 0 : 1;
