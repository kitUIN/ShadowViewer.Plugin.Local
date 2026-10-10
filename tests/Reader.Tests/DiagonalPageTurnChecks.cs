using System.Numerics;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using ShadowViewer.Plugin.Local.Readers.Internal;
using ShadowViewer.Plugin.Local.Readers.Rendering;
using Windows.Foundation;

internal static class DiagonalPageTurnChecks
{
    public static void Run(Action<string, bool, string> check)
    {
        foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
        foreach (float dx in new[] { -350f, 350f })
        foreach (float dy in new[] { -350f, 350f })
        {
            var nodes = Enumerable.Range(0, 9).Select(i => new RenderNode
            {
                PageIndex = i,
                Ctx = new ImageLoadingContext { Size = new Size(1000, 1400) },
                IsSizeLoaded = true,
                Bitmap = new CanvasBitmap(i, new Size(1000, 1400))
            }).ToList();
            var state = new EngineState { CurrentMode = mode };
            new ReaderLayoutService().UpdateActiveLayout(state, nodes, 3, 0, false,
                new Vector2(2000, 1400), new ReaderLayoutCacheState());
            var service = new PageTurnService();
            var delta = new Vector2(dx, dy);
            service.TryCreatePlan(new PageTurnRequest(delta, 0, 1, 3, 9, mode, state.LayoutNodes), out var plan);
            string label = $"{mode} diagonal {delta}";
            int direction = PageTurnService.GetPageDirection(dx < 0, mode);
            check(label + " navigates", plan.TargetPageIndex == 3 + direction * 2 &&
                Math.Sign(plan.CurlTilt) == -Math.Sign(dy), "45 degree drag preserves reading order and tilt");

            CanvasDrawingSession Draw(bool dragging, float amount, float tilt)
            {
                var ds = new CanvasDrawingSession();
                new Win2DPageRenderer().Draw(new PageRenderContext(ds, new Rect(-2000, -2000, 4000, 4000),
                    mode, 1, 1, dragging, !dragging, dragging ? 1 : 0, delta, Vector2.Zero,
                    amount, plan.CurlFromRight, plan.CurlingNode, state.LayoutNodes, nodes, PageTurnCurlTilt: tilt));
                return ds;
            }
            var drag = Draw(true, 0, 0);
            var released = Draw(false, plan.CurrentCurl, plan.CurlTilt);
            check(label + " release is continuous", drag.Polygons.Count > 0 &&
                SamePolygons(drag, released), "drag and first animation frame have identical texture and geometry");
            check(label + " texture tilts", released.Polygons.Any(p => Math.Abs(p.Texture.M12) > 0.01f) &&
                released.Antialiasing == CanvasAntialiasing.Antialiased, "fold rotates texture and restores draw state");
            var receivers = state.LayoutNodes.Where(n => n != plan.CurlingNode)
                .Append(nodes[plan.CurlingNode!.PageIndex + direction * 2]).ToList();
            check(label + " shadow follows fold", released.DiagonalShadows.Count > 0 &&
                released.DiagonalShadows.All(s => Math.Abs(s.End.Y - s.Start.Y) > 0 &&
                    receivers.Any(n => s.Polygon.All(p => Contains(n.Bounds, p)))), "slanted shadow stays on receiving pages");

            var input = new ReaderInputController();
            input.TryHandlePointerPressed(1, new Vector2(800, 400), true, plan.CurrentCurl, 1,
                plan.CurlFromRight, plan.CurlTilt);
            var resumed = new Vector2(800, 400) - input.DragStartPos;
            check(label + " animation takeover retains tilt",
                Math.Abs(PageTurnService.GetCurlTilt(resumed) - plan.CurlTilt) < 0.0001f,
                "virtual drag start preserves both axes");

            service.TryCreatePlan(new PageTurnRequest(delta, 0, 1, 3, 9, mode, state.LayoutNodes, true), out var cancel);
            check(label + " canceled drag rebounds", cancel.TargetCurl == 0 && cancel.TargetPageIndex == 3 &&
                cancel.CurlTilt == plan.CurlTilt, "cancel preserves diagonal pose without changing page");
            var reboundEnd = Draw(false, 0, plan.CurlTilt);
            check(label + " rebound lays flat", reboundEnd.Polygons.Count == 0 &&
                reboundEnd.Images.Any(i => i.Page == plan.CurlingNode.PageIndex), "original page restored at zero");
            var end = Draw(false, plan.TargetCurl, plan.CurlTilt);
            var straightEnd = Draw(false, plan.TargetCurl, 0);
            check(label + " completion lays flat", end.Polygons.Count == 0 &&
                end.Sprites.SequenceEqual(straightEnd.Sprites), "same final spread as horizontal curl");

            // 验证切分覆盖整个纸面，且条带两侧共享同一映射边缘。
            foreach (float progress in new[] { 0.001f, 0.15f, 0.5f, 0.9f, 0.999f })
            {
                var geometry = new PageCurlGeometry(plan.CurlingNode.Bounds, plan.TargetCurl * progress,
                    plan.CurlFromRight, plan.CurlTilt);
                var strips = geometry.GetStrips().Where(s => s.Polygon.Length >= 3).ToList();
                double area = strips.Sum(s => Area(s.Polygon));
                double expected = plan.CurlingNode.Bounds.Width * plan.CurlingNode.Bounds.Height;
                bool connected = true;
                for (int i = 1; i < strips.Count; i++)
                {
                    foreach (var p in strips[i].Polygon)
                    foreach (var q in strips[i - 1].Polygon.Where(q => Vector2.Distance(p, q) < 0.01f))
                        connected &= Vector2.Distance(Vector2.Transform(p, strips[i].Transform),
                            Vector2.Transform(q, strips[i - 1].Transform)) < 0.05f;
                }
                check(label + $" geometry at {progress}", Math.Abs(area - expected) / expected < 0.0001 &&
                    connected && strips.Count <= 42 && strips.SelectMany(s => s.Polygon.Select(p =>
                        Vector2.Transform(p, s.Transform))).All(p => float.IsFinite(p.X) && float.IsFinite(p.Y)),
                    "paper coverage, shared edges and finite bounded geometry");
            }

            // 使用实际控件事件链，避免只验证计划与渲染器而遗漏状态传递。
            var reader = new MangaReader(state, nodes, 3);
            reader.PointerDown(1, new Vector2(1000, 700));
            reader.PointerUp(1, new Vector2(1000, 700) + delta);
            var frame = new CanvasDrawingSession();
            reader.Draw(frame);
            check(label + " control release retains pose", reader.AnimationActive && frame.Polygons.Count > 0,
                "pointer release passes tilt through page-turn lifecycle and frame context");

            var back = nodes[plan.CurlingNode.PageIndex + direction];
            var bitmap = back.Bitmap;
            back.Bitmap = null;
            var missingBack = Draw(false, plan.CurrentCurl, plan.CurlTilt);
            check(label + " missing back uses front texture", missingBack.Polygons.Count > 0 &&
                missingBack.Polygons.All(p => p.Page == plan.CurlingNode.PageIndex),
                "an unloaded back page still curls without gaps or exceptions");
            back.Bitmap = bitmap;
            var oldBounds = back.Bounds;
            float spine = (float)(plan.CurlFromRight ? plan.CurlingNode.Bounds.X :
                plan.CurlingNode.Bounds.X + plan.CurlingNode.Bounds.Width);
            back.Bounds = new Rect(plan.CurlFromRight ? spine - 750 : spine, -450, 750, 900);
            var almostDone = Draw(false, plan.TargetCurl * 0.99999f, plan.CurlTilt);
            var backPoints = almostDone.Polygons.Where(p => p.Page == back.PageIndex).SelectMany(p => p.Polygon).ToList();
            check(label + " unequal back dimensions settle continuously", backPoints.Count > 0 &&
                Math.Abs(backPoints.Min(p => p.X) - back.Bounds.X) < 0.1 &&
                Math.Abs(backPoints.Max(p => p.X) - back.Bounds.X - back.Bounds.Width) < 0.1 &&
                Math.Abs(backPoints.Min(p => p.Y) - back.Bounds.Y) < 0.1 &&
                Math.Abs(backPoints.Max(p => p.Y) - back.Bounds.Y - back.Bounds.Height) < 0.1,
                "near-final diagonal geometry converges to the back page layout");
            back.Bounds = oldBounds;
        }
    }

    private static bool SamePolygons(CanvasDrawingSession a, CanvasDrawingSession b) =>
        a.Polygons.Count == b.Polygons.Count && a.Polygons.Zip(b.Polygons).All(pair =>
            pair.First.Page == pair.Second.Page && pair.First.Texture == pair.Second.Texture &&
            pair.First.Polygon.SequenceEqual(pair.Second.Polygon));

    private static bool Contains(Rect r, Vector2 p) => p.X >= r.X - 0.01 && p.X <= r.X + r.Width + 0.01 &&
        p.Y >= r.Y - 0.01 && p.Y <= r.Y + r.Height + 0.01;

    private static double Area(Vector2[] polygon)
    {
        double twiceArea = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            twiceArea += (double)a.X * b.Y - (double)b.X * a.Y;
        }
        return Math.Abs(twiceArea) / 2;
    }
}
