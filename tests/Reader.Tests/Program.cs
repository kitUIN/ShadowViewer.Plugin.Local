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
        plan.CurlFromRight, plan.CurlingNode, state.LayoutNodes, nodes, PageTurnCurlTilt: plan.CurlTilt));
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
foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
foreach (var boundary in new[] { (Index: 0, Count: 8, Direction: -1), (Index: 7, Count: 8, Direction: 1),
    (Index: 0, Count: 1, Direction: -1) })
{
    var (state, nodes) = Scene(mode, boundary.Index, boundary.Count);
    float dx = boundary.Direction * (mode == ReadingMode.SpreadLtr ? -100 : 100);
    foreach (float dy in new[] { 0f, 60f, -60f })
    {
        var delta = new Vector2(dx, dy);
        string label = $"{mode} empty-side swipe at {boundary.Index}/{boundary.Count}, dy={dy}";
        var drawing = new CanvasDrawingSession();
        renderer.Draw(new PageRenderContext(drawing, new Rect(-1100, -800, 2200, 1600), mode,
            1, 1, true, false, 1, delta, Vector2.Zero, 0, false, null, state.LayoutNodes, nodes));
        Check(label + " keeps the page flat during drag", drawing.Images.Count == 1 &&
            drawing.Images[0].Page == boundary.Index && drawing.Sprites.Count == 0 &&
            drawing.Polygons.Count == 0 && drawing.Shadows.Count == 0 && drawing.DiagonalShadows.Count == 0,
            "no curl, reverse page or shadow on the opposite side");
        var plan = Plan(state, boundary.Index, delta, count: boundary.Count);
        Check(label + " retains only chapter navigation", plan.CurlingNode == null &&
            plan.CurrentCurl == 0 && plan.TargetCurl == 0 && plan.ChapterDirection == boundary.Direction,
            "an outward swipe can request a chapter without animating the page");
        foreach (bool canceled in new[] { false, true })
        {
            var reader = new MangaReader(state, nodes, boundary.Index);
            var requests = new List<int>();
            reader.ChapterNavigationRequested += (_, direction) => requests.Add(direction);
            reader.PointerDown(1, Vector2.Zero);
            reader.PointerMove(1, delta);
            if (canceled) reader.PointerCancel(1);
            else reader.PointerUp(1, delta);
            Check(label + $" release (canceled={canceled}) starts no animation", !reader.AnimationActive &&
                reader.CurrentPageIndex == boundary.Index &&
                requests.SequenceEqual(canceled ? Array.Empty<int>() : new[] { boundary.Direction }),
                "page stays still; only a completed swipe requests a chapter");
        }
    }
    if (boundary.Count > 1)
    {
        var valid = Plan(state, boundary.Index, new Vector2(-dx, 0), count: boundary.Count);
        Check($"{mode} occupied-side swipe at {boundary.Index} still turns", valid.CurlingNode == nodes[boundary.Index] &&
            valid.TargetCurl > 0 && valid.TargetPageIndex != boundary.Index,
            "the first page can advance and the final page can go back");
    }
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

foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    foreach (int sign in new[] { -1, 1 })
    {
        var (state, nodes) = Scene(mode, 3);
        var plan = Plan(state, 3, new Vector2(sign * 10000, 0));
        float maxCurl = (float)plan.CurlingNode!.Bounds.Width * 2;
        var clamped = DrawPlan(state, nodes, plan, maxCurl);
        var excess = DrawPlan(state, nodes, plan, maxCurl * 5);
        var dragging = new CanvasDrawingSession();
        renderer.Draw(new PageRenderContext(dragging, new Rect(-1100, -800, 2200, 1600), mode,
            1, 1, true, false, 1, new Vector2(sign * 10000, 0), Vector2.Zero, 0, false, null, state.LayoutNodes, nodes));
        Check($"{mode} {sign} excessive curl stops at page distance",
            plan.CurrentCurl == maxCurl && plan.TargetCurl == maxCurl &&
            excess.Sprites.SequenceEqual(clamped.Sprites) && dragging.Sprites.SequenceEqual(clamped.Sprites),
            $"drag and animation capped at {maxCurl}");
        var step = service.StepAnimation(plan.CurrentCurl, plan.TargetCurl, plan.AnimVelocity, 1f / 60);
        Check($"{mode} {sign} release at limit completes without moving back",
            step.IsFinished && step.CurlAmount == maxCurl && step.Velocity == 0,
            $"curl {step.CurlAmount}");
    }
}

foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    foreach (int sign in new[] { -1, 1 })
    {
        var (state, nodes) = Scene(mode, 3);
        var plan = Plan(state, 3, new Vector2(sign * 100, 0));
        var mid = DrawPlan(state, nodes, plan, plan.TargetCurl / 2);
        var nearEnd = DrawPlan(state, nodes, plan, plan.TargetCurl * 0.98f);
        var end = DrawPlan(state, nodes, plan, plan.TargetCurl);
        var start = DrawPlan(state, nodes, plan, 0);
        bool clipped = mid.Shadows.All(s => nodes.Where(n => n != plan.CurlingNode).Any(n =>
            s.Bounds.X >= n.Bounds.X && s.Bounds.X + s.Bounds.Width <= n.Bounds.X + n.Bounds.Width &&
            s.Bounds.Y >= n.Bounds.Y && s.Bounds.Y + s.Bounds.Height <= n.Bounds.Y + n.Bounds.Height));
        Check($"{mode} {sign} curl shadow is clipped and fades at endpoints",
            mid.Shadows.Count > 0 && clipped && start.Shadows.Count == 0 && end.Shadows.Count == 0 &&
            nearEnd.Shadows.All(s => s.Stops[1].Color.A < mid.Shadows[0].Stops[1].Color.A) &&
            mid.Shadows.All(s => s.Stops[0].Color.A == 0 && s.Stops[^1].Color.A == 0),
            $"mid-turn shadows {mid.Shadows.Count}, clipped {clipped}");
        Check($"{mode} {sign} completed page has no residual dark tint",
            end.Tints.All(t => t == Vector4.One), "back page returns to full brightness");
    }
}

foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    foreach (var transition in new[] { (From: 0, To: 1), (From: 1, To: 0), (From: 3, To: 5), (From: 3, To: 1), (From: 7, To: 8) })
    {
        var (state, nodes) = Scene(mode, transition.From, transition.To == 8 ? 8 + 1 : 9);
        bool created = service.TryCreateAutomaticPlan(transition.From, transition.To, nodes.Count, mode, 1, 1, state.LayoutNodes, out var plan);
        // 7 和 8 属于同一双页，不应播放动画。
        bool sameSpread = transition.From == 7;
        Check($"{mode} automatic page request {transition.From}->{transition.To}", created != sameSpread &&
            (!created || plan.CurrentCurl == 0 && plan.TargetPageIndex == transition.To &&
                PageTurnService.GetPageDirection(plan.CurlFromRight, mode) == Math.Sign(transition.To - transition.From)),
            $"created {created}, target {plan.TargetPageIndex}");
        if (!created) continue;
        float amount = plan.CurrentCurl, speed = plan.AnimVelocity;
        bool finished = false;
        for (int frame = 0; frame < 60 && !finished; frame++)
        {
            var step = service.StepAnimation(amount, plan.TargetCurl, speed, 1f / 60);
            amount = step.CurlAmount; speed = step.Velocity; finished = step.IsFinished;
        }
        Check($"{mode} automatic {transition.From}->{transition.To} converges", finished && amount == plan.TargetCurl,
            $"starts at zero, ends at {amount}");
    }
    var (currentState, _) = Scene(mode, 3);
    Check($"{mode} automatic animation rejects jumps, zoom and invalid requests",
        !service.TryCreateAutomaticPlan(3, 8, 9, mode, 1, 1, currentState.LayoutNodes, out _) &&
        !service.TryCreateAutomaticPlan(3, 5, 9, mode, 2, 1, currentState.LayoutNodes, out _) &&
        !service.TryCreateAutomaticPlan(3, -1, 9, mode, 1, 1, currentState.LayoutNodes, out _) &&
        !service.TryCreateAutomaticPlan(3, 9, 9, mode, 1, 1, currentState.LayoutNodes, out _) &&
        !service.TryCreateAutomaticPlan(5, 7, 9, mode, 1, 1, currentState.LayoutNodes, out _),
        "direct navigation remains available outside adjacent unzoomed spreads");
}

foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    var (state, nodes) = Scene(mode, 3);
    var control = new MangaReader(state, nodes, 3);
    control.CurrentPageIndex = 5;
    Check($"{mode} automatic request preserves old layout until completion",
        control.AnimationActive && control.TargetIndex == 5 && state.LayoutNodes.Any(n => n.PageIndex == 3),
        "requested index 5, visible spread remains 3/4 during animation");
    control.FinishAnimation();
    Check($"{mode} automatic completion applies layout exactly once",
        !control.AnimationActive && control.CurrentPageIndex == 5 && state.LayoutNodes.All(n => n.PageIndex is 5 or 6),
        "completion does not restart an automatic animation");
    control.StartGesture(Plan(state, 5, new Vector2(mode == ReadingMode.SpreadLtr ? -100 : 100, 0)));
    control.FinishAnimation();
    Check($"{mode} gesture completion does not replay automatic animation",
        !control.AnimationActive && control.CurrentPageIndex == 7 && state.LayoutNodes.All(n => n.PageIndex is 7 or 8),
        "gesture target becomes the final visible spread");
    control.CurrentPageIndex = 5;
    control.CurrentPageIndex = 1;
    Check($"{mode} a new request cancels an in-flight automatic turn",
        !control.AnimationActive && control.CurrentPageIndex == 1 && state.LayoutNodes.All(n => n.PageIndex is 1 or 2),
        "latest request wins and applies immediately");
    control.CurrentPageIndex = 3;
    control.InterruptAnimation();
    Check($"{mode} pointer takeover restores the visible source index",
        !control.AnimationActive && control.CurrentPageIndex == 1 && state.LayoutNodes.All(n => n.PageIndex is 1 or 2),
        "continued drag uses source spread 1/2 rather than pending target 3/4");
}

foreach (var mode in new[] { ReadingMode.VerticalScroll, ReadingMode.SinglePage, ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    foreach (bool allowHorizontal in new[] { false, true })
    {
        double time = 0;
        var input = new ReaderInputController(() => time);
        var state = new EngineState { CurrentMode = mode };
        var frame = new ReaderFrameOrchestrator();
        input.TryHandlePointerPressed(1, new Vector2(600, 500), false, 0, 1, false);
        input.TryHandlePointerPressed(2, new Vector2(1000, 500), false, 0, 1, false);
        time = 0.016;
        input.HandlePointerMoved(1, new Vector2(660, 540));
        input.HandlePointerMoved(2, new Vector2(1060, 540));
        var delta = input.ConsumeFrameDelta();
        frame.Step(state, delta, 0.016f, 1, new Vector2(2000, 1400), true, allowHorizontal,
            false, 0, 0, 0, Vector2.Zero, service);
        float expectedX = mode == ReadingMode.VerticalScroll && !allowHorizontal ? 0 : -60;
        Check($"{mode} two-finger pan (horizontal {allowHorizontal})",
            Vector2.Distance(state.CameraPos, new Vector2(expectedX, -40)) < 0.001f &&
            Math.Abs(state.Zoom - 1) < 0.0001f && !delta.IsPageTurnGesture,
            $"camera {state.CameraPos}, zoom {state.Zoom}; constant finger distance");
        input.HandlePointerLost(2);
        input.HandlePointerMoved(1, new Vector2(680, 560));
        var remaining = input.ConsumeFrameDelta();
        Check($"{mode} remaining finger continues to pan",
            remaining.PanDelta == new Vector2(20, 20) && !remaining.IsPageTurnGesture && !input.IsPageTurnGesture,
            "lifting one finger does not introduce a jump or a page-turn gesture");
        var release = input.HandlePointerLost(1);
        Check($"{mode} two-finger release cannot navigate", !release.IsPageTurnGesture && input.ActivePointerCount == 0,
            "gesture identity retained through sequential release");
    }
}
{
    var input = new ReaderInputController();
    var state = new EngineState { CurrentMode = ReadingMode.SpreadLtr };
    var frame = new ReaderFrameOrchestrator();
    var view = new Vector2(2000, 1400);
    var initialCenter = new Vector2(800, 500);
    var worldAnchor = initialCenter - view / 2;
    input.TryHandlePointerPressed(1, new Vector2(600, 500), false, 0, 1, false);
    input.TryHandlePointerPressed(2, new Vector2(1000, 500), false, 0, 1, false);
    input.HandlePointerMoved(1, new Vector2(500, 540));
    input.HandlePointerMoved(2, new Vector2(1200, 540));
    var delta = input.ConsumeFrameDelta();
    frame.Step(state, delta, 1f / 60, 1, view, true, true, false, 0, 0, 0, Vector2.Zero, service);
    var screenAnchor = view / 2 + (worldAnchor - state.CameraPos) * state.Zoom;
    Check("Combined pinch and pan anchors content to finger centroid",
        Math.Abs(state.Zoom - 1.75f) < 0.001f && Vector2.Distance(screenAnchor, new Vector2(850, 540)) < 0.001f,
        $"zoom {state.Zoom}, original content anchor now at {screenAnchor}");
    input.HandlePointerMoved(1, new Vector2(570, 560));
    input.HandlePointerMoved(2, new Vector2(1270, 560));
    var next = input.ConsumeFrameDelta();
    frame.Step(state, next, 1f / 60, 1, view, true, true, false, 0, 0, 0, Vector2.Zero, service);
    screenAnchor = view / 2 + (worldAnchor - state.CameraPos) * state.Zoom;
    Check("Pinch and pan remain anchored across frame boundaries",
        Math.Abs(state.Zoom - 1.75f) < 0.001f && Vector2.Distance(screenAnchor, new Vector2(920, 560)) < 0.001f,
        $"anchor {screenAnchor}, no extra scaling while translating");
    state.Velocity = Vector2.Zero;
    float releasedZoom = state.Zoom;
    frame.Step(state, new InputFrameDelta(Vector2.Zero, 1, Vector2.Zero, false, Vector2.Zero, false),
        1f / 60, 1, view, false, true, false, 0, 0, 0, Vector2.Zero, service);
    Check("Pinch stops scaling without zoom inertia", state.Zoom == releasedZoom && state.ZoomVelocity == 0,
        $"released zoom {state.Zoom}, zoom velocity {state.ZoomVelocity}");
    input.TryHandlePointerPressed(3, new Vector2(1500, 800), false, 0, 1, false);
    input.HandlePointerMoved(1, new Vector2(590, 570));
    input.HandlePointerLost(3);
    var rebased = input.ConsumeFrameDelta();
    Check("Three-to-two pointer transition resets pinch baseline",
        rebased.PanDelta == Vector2.Zero && rebased.ZoomDelta == 1,
        "resuming two fingers introduces no phantom movement or scaling");
}
foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
{
    var (state, nodes) = Scene(mode, 3);
    var drawing = new CanvasDrawingSession();
    renderer.Draw(new PageRenderContext(drawing, new Rect(-1100, -800, 2200, 1600), mode, 1, 1,
        true, false, 1, new Vector2(-300, 0), Vector2.Zero, 0, false, null, state.LayoutNodes, nodes, false));
    Check($"{mode} remaining finger never renders a curl after multitouch",
        drawing.Sprites.Count == 0 && drawing.Shadows.Count == 0 && drawing.Images.Count == 2,
        "renderer keeps the normal spread while a two-finger gesture becomes one finger");
}

{
    double time = 0;
    var input = new ReaderInputController(() => time);
    input.TryHandlePointerPressed(1, new Vector2(600, 500), false, 0, 1, false);
    input.TryHandlePointerPressed(2, new Vector2(1000, 500), false, 0, 1, false);
    time = 0.016;
    input.HandlePointerMoved(1, new Vector2(660, 540));
    time = 0.01601;
    input.HandlePointerMoved(2, new Vector2(1060, 540));
    input.HandlePointerLost(2);
    var release = input.HandlePointerLost(1);
    Check("Closely spaced touch events do not spike release velocity", release.Velocity.Length() < 5000,
        $"second pointer arrived 10 microseconds later; release speed {release.Velocity.Length():F0}px/s");
    time = 0;
    input.Reset();
    input.TryHandlePointerPressed(1, new Vector2(600, 500), false, 0, 1, false);
    input.TryHandlePointerPressed(2, new Vector2(1000, 500), false, 0, 1, false);
    time = 0.016;
    input.HandlePointerMoved(1, new Vector2(500, 500));
    time = 0.01601;
    input.HandlePointerMoved(2, new Vector2(1100, 500));
    input.HandlePointerLost(2);
    Check("Stationary-centroid pinch cannot launch pan inertia", input.HandlePointerLost(1).Velocity == Vector2.Zero,
        "symmetric pinch retains zero translation velocity at release");
}

foreach (int hz in new[] { 30, 60, 120, 144 })
{
    var input = new ReaderInputController();
    var frame = new ReaderFrameOrchestrator();
    var state = new EngineState { Zoom = 2 };
    input.HandleWheel(-120);
    float first = input.ConsumeFrameDelta(1f / hz).PanDelta.Y;
    Check($"Wheel starts smoothly at {hz} Hz", first < 0 && first > -120, $"first step {first}");
    float second = input.ConsumeFrameDelta(1f / hz).PanDelta.Y;
    Check($"Wheel accelerates instead of jumping at {hz} Hz", Math.Abs(second) > Math.Abs(first),
        $"first step {first}, second step {second}");
    input.Reset();
    input.HandleWheel(-120);
    input.HandleWheel(-120);
    for (int i = 0; i < hz; i++)
    {
        var delta = input.ConsumeFrameDelta(1f / hz);
        frame.Step(state, delta, 1f / hz, 1, new Vector2(1000, 1400), false, false,
            false, 0, 0, 0, Vector2.Zero, service);
    }
    Check($"Wheel preserves accumulated distance and zoom at {hz} Hz",
        Math.Abs(state.CameraPos.Y - 120) < 0.001f && state.Velocity == Vector2.Zero,
        $"camera {state.CameraPos}, velocity {state.Velocity}");
    Check($"Wheel settles at {hz} Hz", input.ConsumeFrameDelta(1f / hz).PanDelta == Vector2.Zero && !input.HasPendingWheel,
        "no residual movement after one second");
}
{
    var continued = new ReaderInputController();
    var baseline = new ReaderInputController();
    continued.HandleWheel(-120);
    baseline.HandleWheel(-120);
    continued.ConsumeFrameDelta(0.08f);
    baseline.ConsumeFrameDelta(0.08f);
    continued.HandleWheel(-120);
    float continuedStep = continued.ConsumeFrameDelta(0.0001f).PanDelta.Y;
    float baselineStep = baseline.ConsumeFrameDelta(0.0001f).PanDelta.Y;
    Check("Repeated wheel notch preserves velocity continuity",
        Math.Abs(continuedStep / baselineStep - 1) < 0.01f,
        $"step with new notch {continuedStep}, without notch {baselineStep}");

    float Travel(params float[] intervals)
    {
        var input = new ReaderInputController();
        input.HandleWheel(-120);
        return intervals.Sum(dt => input.ConsumeFrameDelta(dt).PanDelta.Y);
    }
    float uniform = Travel(Enumerable.Repeat(0.01f, 20).ToArray());
    float uneven = Travel(0.007f, 0.023f, 0.011f, 0.059f, 0.1f);
    Check("Wheel trajectory survives uneven frame timing", Math.Abs(uniform - uneven) < 0.001f,
        $"uniform frames {uniform}, uneven frames {uneven}");
    var pending = new ReaderInputController();
    pending.HandleWheel(-1200);
    pending.ConsumeFrameDelta(0.2f);
    Check("Layout stays deferred until wheel tail finishes", pending.HasPendingWheel,
        "layout cannot change while the scroll animation is running");
    pending.ConsumeFrameDelta(0.05f);
    Check("Wheel reaches its target without a long spring tail", !pending.HasPendingWheel,
        "even a large burst finishes within 250 ms of its last input");
    pending.Reset();
    Check("Reset clears pending wheel layout guard", !pending.HasPendingWheel,
        "canceled scrolling no longer delays layout");
}
{
    var input = new ReaderInputController();
    input.HandleWheel(-360);
    input.ConsumeFrameDelta(1f / 60);
    input.HandleWheel(120);
    Check("Wheel reversal responds on next frame", input.ConsumeFrameDelta(1f / 60).PanDelta.Y > 0,
        "old direction is discarded");
    input.Reset();
    Check("Input reset cancels wheel animation", input.ConsumeFrameDelta(1f / 60).PanDelta == Vector2.Zero,
        "mode, content and zoom resets leave no wheel movement");
    input.HandleWheel(-120);
    input.TryHandlePointerPressed(1, Vector2.Zero, false, 0, 1, false);
    input.HandleWheel(-120);
    input.HandlePointerMoved(1, new Vector2(0, 20));
    Check("Dragging takes over wheel animation", input.ConsumeFrameDelta(1f / 60).PanDelta == new Vector2(0, 20),
        "only pointer movement is applied");
}

foreach (int hz in new[] { 60, 120, 144 })
{
    var wheel = new ReaderWheelScrollController();
    float total = 0;
    var steps = new List<float>();
    int notches = 0;
    for (int i = 0; i < hz * 2; i++)
    {
        if ((double)i / hz >= notches * 0.1)
        {
            wheel.AddDelta(-120);
            notches++;
        }
        float step = wheel.ConsumeDelta(1f / hz);
        total += step;
        if (i >= hz) steps.Add(Math.Abs(step));
    }
    Check($"Continuous wheel cadence avoids stop-start pulses at {hz} Hz",
        steps.Min() > 0 && steps.Max() / steps.Min() < 1.5f,
        $"frame distances {steps.Min():F2}..{steps.Max():F2}");
    total += wheel.ConsumeDelta(0.25f);
    Check($"Continuous wheel conserves distance at {hz} Hz",
        Math.Abs(total + notches * 120) < 0.01f && !wheel.IsActive,
        $"{notches} notches, total {total}");
}
{
    double now = 0;
    var input = new ReaderInputController(() => now);
    foreach (float delta in new[] { -1f, -4f, -12f, -120f, -240f, -30f, -9f, -2f, 3f, 0.5f })
    {
        input.HandleWheel(delta);
        float applied = input.ConsumeFrameDelta(1f / 120).PanDelta.Y;
        Check($"Precision scroll follows delta {delta} without added easing", applied == delta,
            $"applied {applied} on the next frame");
        now += 1.0 / 120;
    }
    Check("Precision gesture gaps keep layout stable", input.IsWheelInteracting,
        "no layout refresh between device samples");
    now += 0.11;
    Check("Precision scroll adds no synthetic inertia",
        !input.IsWheelInteracting && input.ConsumeFrameDelta(1f / 60).PanDelta == Vector2.Zero,
        "device inertia ends when the device stops sending deltas");
    input.HandleWheel(-120);
    input.ConsumeFrameDelta(1f / 60);
    input.HandleWheel(5);
    input.HandleWheel(7);
    Check("Precision input takes over a pending mouse animation",
        input.ConsumeFrameDelta(1f / 60).PanDelta.Y == 12 && !input.HasPendingWheel,
        "new samples accumulate; old animation does not move the view");
}

{
    var (state, nodes) = Scene(ReadingMode.VerticalScroll, 0, 1000);
    var control = new MangaReader(state, nodes, 0);
    foreach (int index in new[] { 999, 5, 900, 20, 700, 100, 999 }) control.CurrentPageIndex = index;
    Check("Vertical progress jumps reuse the chapter layout", control.LayoutUpdates == 0 && control.CurrentPageIndex == 999,
        "repeated slider changes do not recalculate every page or acquire chapter-wide layout locks");
}

// Pause the UI completion callback while a real render/cache worker tries to read the frame.
// It must wait before reading any layout or camera state, then observe only the target spread.
foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
foreach (bool automatic in new[] { false, true })
foreach (var transition in new[] { (From: 3, To: 5), (From: 5, To: 3) })
foreach (bool refreshImages in new[] { false, true })
{
    var (state, nodes) = Scene(mode, transition.From);
    var control = new MangaReader(state, nodes, transition.From);
    if (automatic) control.CurrentPageIndex = transition.To;
    else
    {
        float delta = Math.Sign(transition.To - transition.From) * (mode == ReadingMode.SpreadLtr ? -100 : 100);
        control.StartGesture(Plan(state, transition.From, new Vector2(delta, 0)));
    }
    var targetBitmaps = nodes.Skip(transition.To).Take(2).Select(n => n.Bitmap).ToArray();
    var drawing = new CanvasDrawingSession();
    using var started = new ManualResetEventSlim();
    Exception? workerError = null;
    var worker = new Thread(() =>
    {
        started.Set();
        try
        {
            if (refreshImages) control.RefreshImages();
            else control.Draw(drawing);
        }
        catch (Exception ex) { workerError = ex; }
    }) { IsBackground = true };
    bool waited;
    lock (control.FrameGate)
    {
        worker.Start();
        bool running = started.Wait(TimeSpan.FromSeconds(5));
        waited = running && SpinWait.SpinUntil(() => !worker.IsAlive ||
            (worker.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)) && worker.IsAlive;
        control.FinishAnimation();
    }
    bool completed = worker.Join(TimeSpan.FromSeconds(5));
    if (refreshImages && completed && workerError == null) control.Draw(drawing);
    Check($"{mode} {(automatic ? "automatic" : "gesture")} {transition.From}->{transition.To} {(refreshImages ? "cache" : "draw")} handoff",
        waited && completed && workerError == null && drawing.Sprites.Count == 0 &&
        drawing.Images.Select(i => i.Page).Order().SequenceEqual(new[] { transition.To, transition.To + 1 }) &&
        targetBitmaps.Select((bitmap, i) => ReferenceEquals(bitmap, nodes[transition.To + i].Bitmap) && !bitmap!.IsDisposed).All(same => same),
        $"waited={waited}, completed={completed}, pages={string.Join(",", drawing.Images.Select(i => i.Page))}, error={workerError?.Message}");
}

ChapterNavigationChecks.Run(Check);
DiagonalPageTurnChecks.Run(Check);
await ImageLoadingChecks.RunAsync(Check);

Console.WriteLine($"TOTAL {passed + failed}: PASS {passed}, FAIL {failed}");
Console.WriteLine("Uses production source links; Windows/Win2D adapters record draw calls only, no native GPU or UI validation.");
Environment.ExitCode = failed == 0 ? 0 : 1;
