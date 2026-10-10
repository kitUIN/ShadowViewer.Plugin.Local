using System.Numerics;
using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Plugin.Local.Readers.Internal;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using Windows.Foundation;

internal static class ChapterNavigationChecks
{
    public static void Run(Action<string, bool, string> check)
    {
        foreach (var mode in Enum.GetValues<ReadingMode>())
        foreach (int count in new[] { 1, 2, 8, 9 })
        {
            var nodes = Enumerable.Range(0, count).Select(i => new RenderNode
            {
                PageIndex = i,
                Ctx = new ImageLoadingContext { Size = new Size(1000, 1400) },
                IsSizeLoaded = true,
                Bounds = new Rect(0, i * 1400, 1000, 1400)
            }).ToList();
            var state = new EngineState { CurrentMode = mode };
            var reader = new MangaReader(state, nodes, 0);
            var requests = new List<int>();
            reader.ChapterNavigationRequested += (_, direction) => requests.Add(direction);
            reader.TurnPage(-1);
            check($"{mode}/{count} previous chapter requires a backward request", requests.SequenceEqual(new[] { -1 }),
                "opening the first page is silent; continuing backward requests previous chapter");
            requests.Clear();
            while (!ReaderNavigationService.IsPageBoundary(reader.CurrentPageIndex, count, mode, 1))
            {
                reader.TurnPage(1);
                if (reader.AnimationActive) reader.FinishAnimation();
            }
            check($"{mode}/{count} arriving at last page is silent", requests.Count == 0,
                $"last visible group contains final page; current index {reader.CurrentPageIndex}");
            int lastIndex = reader.CurrentPageIndex;
            reader.TurnPage(1);
            reader.TurnPage(1);
            check($"{mode}/{count} continuing forward can request next chapter again",
                requests.SequenceEqual(new[] { 1, 1 }) && reader.CurrentPageIndex == lastIndex,
                "no page change at boundary; a fresh input can reopen a dismissed tip");
        }

        foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
        {
            var service = new PageTurnService();
            var nodes = new List<RenderNode>
            {
                new() { PageIndex = 7, Bounds = new Rect(-1000, -700, 1000, 1400) },
                new() { PageIndex = 8, Bounds = new Rect(0, -700, 1000, 1400) }
            };
            float forward = mode == ReadingMode.SpreadLtr ? -100 : 100;
            foreach (int index in new[] { 7, 8 })
            {
                service.TryCreatePlan(new PageTurnRequest(new Vector2(forward, 0), 0, 1, index, 9, mode, nodes), out var plan);
                check($"{mode} final spread swipe from index {index} requests next chapter",
                    plan.ChapterDirection == 1 && plan.TargetPageIndex == index && plan.TargetCurl == 0,
                    "either page index of the final spread is a chapter boundary");
            }
            service.TryCreatePlan(new PageTurnRequest(new Vector2(-forward, 0), 0, 1, 0, 9, mode, nodes), out var previous);
            check($"{mode} cover swipe requests previous chapter", previous.ChapterDirection == -1, "reading direction is respected");
            foreach (var delta in new[] { new Vector2(forward / 5, 0), new Vector2(forward, 200) })
            {
                service.TryCreatePlan(new PageTurnRequest(delta, 0, 1, 7, 9, mode, nodes), out var plan);
                check($"{mode} short or vertical gesture does not request a chapter {delta}", plan.ChapterDirection == 0,
                    "the ordinary swipe threshold applies to chapter requests too");
            }
            service.TryCreatePlan(new PageTurnRequest(new Vector2(forward, 0), 2000, 1, 7, 9, mode, nodes, true), out var canceled);
            check($"{mode} canceled boundary swipe is silent", canceled.ChapterDirection == 0, "canceled input only rebounds");
        }

        var scroll = new EngineState { CurrentMode = ReadingMode.VerticalScroll };
        scroll.LayoutNodes.Add(new RenderNode { Bounds = new Rect(0, 0, 1000, 2000) });
        scroll.LayoutNodes.Add(new RenderNode { Bounds = new Rect(0, 2000, 1000, 2000) });
        var view = new Vector2(1000, 1000);
        scroll.CameraPos.Y = 3000;
        check("Vertical last page is not yet the bottom", !ReaderNavigationService.IsScrollBoundary(scroll, view, 1),
            "the remaining half of a tall final page must still be read");
        scroll.CameraPos.Y = 3500;
        check("Vertical final content edge is the bottom", ReaderNavigationService.IsScrollBoundary(scroll, view, 1),
            "the viewport has reached the actual chapter end");
        scroll.CameraPos.Y = 500;
        check("Vertical first content edge is the top", ReaderNavigationService.IsScrollBoundary(scroll, view, -1),
            "the viewport has reached the chapter start");
        check("A vertical swipe that only arrives at the bottom is silent",
            ReaderNavigationService.GetScrollSwipeDirection(new Vector2(0, -500), false, false, false) == 0,
            "boundary state is captured when the gesture starts, not after scrolling or inertia");
        check("A new outward vertical swipe requests the matching chapter",
            ReaderNavigationService.GetScrollSwipeDirection(new Vector2(0, -100), false, true, false) == 1 &&
            ReaderNavigationService.GetScrollSwipeDirection(new Vector2(0, 100), true, false, false) == -1,
            "both start and end are supported");
        check("Canceled or short vertical swipes are silent",
            ReaderNavigationService.GetScrollSwipeDirection(new Vector2(0, -100), false, true, true) == 0 &&
            ReaderNavigationService.GetScrollSwipeDirection(new Vector2(0, -10), false, true, false) == 0,
            "no chapter request on cancellation or touch jitter");
        scroll.LayoutNodes.Clear();
        var empty = new MangaReader(scroll, [], 0);
        int emptyRequests = 0;
        empty.ChapterNavigationRequested += (_, _) => emptyRequests++;
        empty.TurnPage(1);
        empty.TurnPage(-1);
        check("Empty chapters do not request navigation", emptyRequests == 0 &&
            !ReaderNavigationService.IsScrollBoundary(scroll, view, 1), "loading or empty content is silent");

        CheckInput(check);
    }

    private static void CheckInput(Action<string, bool, string> check)
    {
        foreach (var mode in Enum.GetValues<ReadingMode>())
        {
            var nodes = Enumerable.Range(0, 9).Select(i => new RenderNode
            {
                PageIndex = i,
                Ctx = new ImageLoadingContext { Size = new Size(1000, 1400) },
                IsSizeLoaded = true,
                Bounds = new Rect(0, i * 1400, 1000, 1400)
            }).ToList();
            var state = new EngineState { CurrentMode = mode };
            new ReaderLayoutService().UpdateActiveLayout(state, nodes, 8, 0, false,
                new Vector2(2000, 1400), new ReaderLayoutCacheState());
            var reader = new MangaReader(state, nodes, 8);
            var requests = new List<int>();
            reader.ChapterNavigationRequested += (_, direction) => requests.Add(direction);
            state.CameraPos.Y = (float)(nodes[^1].Bounds.Y + nodes[^1].Bounds.Height - 700);
            reader.Wheel(-120);
            check($"{mode} native wheel handler requests next chapter", requests.SequenceEqual(new[] { 1 }),
                "wheel input reaches the shared boundary event");
            requests.Clear();
            var delta = mode == ReadingMode.VerticalScroll ? new Vector2(0, -100)
                : new Vector2(mode == ReadingMode.SpreadRtl ? 100 : -100, 0);
            reader.PointerDown(1, Vector2.Zero);
            reader.PointerUp(1, delta);
            check($"{mode} native release handler requests next chapter", requests.SequenceEqual(new[] { 1 }),
                "a new outward gesture triggers exactly one request");
            if (reader.AnimationActive) reader.FinishAnimation();
            requests.Clear();
            reader.PointerDown(1, Vector2.Zero);
            reader.PointerMove(1, delta);
            reader.PointerCancel(1);
            check($"{mode} canceled native gesture does not prompt", requests.Count == 0, "capture cancellation is silent");
            if (reader.AnimationActive) reader.FinishAnimation();
            state.Zoom = 2;
            reader.PointerDown(1, Vector2.Zero);
            reader.PointerUp(1, delta);
            check($"{mode} zoomed native pan does not prompt", requests.Count == 0, "zoomed dragging remains panning");
            state.Zoom = 1;
            reader.PointerDown(1, Vector2.Zero);
            reader.PointerDown(2, new Vector2(100, 100));
            reader.PointerUp(2, new Vector2(100, 100) + delta);
            reader.PointerUp(1, delta);
            check($"{mode} multiple-pointer native gesture does not prompt", requests.Count == 0,
                "pinch interactions cannot trigger chapter navigation");
        }
    }
}
