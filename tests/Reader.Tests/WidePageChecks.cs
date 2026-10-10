using System.Numerics;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using ShadowViewer.Plugin.Local.Readers.Internal;
using ShadowViewer.Plugin.Local.Readers.Rendering;
using Windows.Foundation;

internal static class WidePageChecks
{
    private static List<RenderNode> Sources(params double[] widths) => widths.Select((width, i) => new RenderNode
    {
        PageIndex = i,
        Source = i,
        Ctx = new ImageLoadingContext { Size = new Size(width, 1400) },
        IsSizeLoaded = true,
        Bitmap = new CanvasBitmap(i, new Size(width, 1400))
    }).ToList();

    private static void Layout(EngineState state, List<RenderNode> pages, int index, bool fit = false) =>
        new ReaderLayoutService().UpdateActiveLayout(state, pages, index, 0, fit,
            new Vector2(2000, 1400), new ReaderLayoutCacheState());

    public static void Run(Action<string, bool, string> check)
    {
        foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
        {
            var sources = Sources(1000, 1000, 2000, 2000, 1000);
            var pages = new List<RenderNode>();
            ReaderPageSequence.Update(sources, pages, mode, 0);
            double firstX = mode == ReadingMode.SpreadLtr ? 0 : 0.5;
            check($"{mode} wide images become ordered half pages", pages.Count == 7 &&
                pages.Select(n => (int)n.Source).SequenceEqual(new[] { 0, 1, 2, 2, 3, 3, 4 }) &&
                pages[2].SourceRegion.X == firstX && pages[3].SourceRegion.X == 0.5 - firstX &&
                pages[2].PageSize.Width == 1000 && pages[2].PageSize.Height == 1400,
                "left half first in LTR, right half first in RTL; original image remains shared");

            var state = new EngineState { CurrentMode = mode };
            foreach (bool fit in new[] { false, true })
            {
                Layout(state, pages, 1, fit);
                check($"{mode}/{fit} first half pairs with preceding normal page",
                    state.LayoutNodes.Select(n => n.PageIndex).Order().SequenceEqual(new[] { 1, 2 }) &&
                    state.LayoutNodes.All(n => n.Bounds.Width == 1000 && n.Bounds.Height == 1400),
                    "a wide image may span two different spreads without shrinking the halves");
                Layout(state, pages, 3, fit);
                check($"{mode}/{fit} consecutive wide images pair across source boundaries",
                    state.LayoutNodes.Select(n => n.PageIndex).Order().SequenceEqual(new[] { 3, 4 }),
                    "second half of one image pairs with first half of the next");
            }

            Layout(state, pages, 0);
            var reader = new MangaReader(state, pages, 0);
            int chapterRequests = 0;
            reader.ChapterNavigationRequested += (_, _) => chapterRequests++;
            var seen = new List<int> { 0 };
            for (int i = 0; i < 3; i++)
            {
                reader.TurnPage(1);
                if (reader.AnimationActive) reader.FinishAnimation();
                seen.AddRange(state.LayoutNodes.Select(n => n.PageIndex));
            }
            check($"{mode} navigation visits both halves exactly once",
                seen.Order().SequenceEqual(Enumerable.Range(0, 7)) && chapterRequests == 0,
                "logical page count controls navigation beyond the original image count");
            reader.TurnPage(1);
            check($"{mode} chapter boundary follows final logical page", chapterRequests == 1,
                "next chapter is requested only after all seven reading pages");
            for (int i = 0; i < 3; i++)
            {
                reader.TurnPage(-1);
                if (reader.AnimationActive) reader.FinishAnimation();
            }
            check($"{mode} backward navigation returns to cover", reader.CurrentPageIndex == 0,
                "backward turns remain symmetric after inserting half pages");

            foreach (int index in new[] { 1, 3, 5 })
            {
                Layout(state, pages, index);
                var ds = new CanvasDrawingSession();
                new MangaReader(state, pages, index).Draw(ds);
                check($"{mode} flat rendering crops spread {index}", ds.CroppedImages.Count == 2 &&
                    state.LayoutNodes.All(n => ds.CroppedImages.Any(image => image.Page == (int)n.Source &&
                        image.Source.Equals(n.GetSourceRect(n.Bitmap!)) && image.Bounds.Equals(n.Bounds))),
                    "drawing samples only the assigned half, using the shared original bitmap");
            }

            CheckCurl(check, mode, pages);
            CheckChanges(check, mode);
            CheckScope(check, mode);
            CheckImageWindow(check, mode);
        }

        foreach (var mode in new[] { ReadingMode.SinglePage, ReadingMode.VerticalScroll })
        {
            var sources = Sources(1000, 2000, 1000);
            var pages = new List<RenderNode>();
            ReaderPageSequence.Update(sources, pages, mode, 0);
            check($"{mode} keeps wide images whole", pages.SequenceEqual(sources) && pages[1].PageSize.Width == 2000,
                "only double-page reading splits images");
        }
    }

    private static void CheckScope(Action<string, bool, string> check, ReadingMode mode)
    {
        var sources = Sources(1000, 2000, 2000);
        sources[0].ImageStrategy = new LocalStrategy();
        sources[1].ImageStrategy = new LocalStrategy();
        sources[2].ImageStrategy = new NoopStrategy();
        sources[2].Ctx.CachedFilePath = @"C:\cache\network-wide.jpg";
        var pages = new List<RenderNode>();
        ReaderPageSequence.Update(sources, pages, mode, 0, scope: WidePageSplitScope.LocalFilesOnly);
        double firstX = mode == ReadingMode.SpreadLtr ? 0 : 0.5;
        check($"{mode} local scope splits only local originals", pages.Count == 4 &&
            pages[1].ImageNode == sources[1] && pages[2].ImageNode == sources[1] &&
            pages[1].SourceRegion.X == firstX && pages[2].SourceRegion.X == 0.5 - firstX &&
            pages[3] == sources[2] && pages[3].PageSize.Width == 2000,
            "a network image remains whole even when its bytes are cached on disk");

        int current = ReaderPageSequence.Update(sources, pages, mode, 3, scope: WidePageSplitScope.AllImages);
        check($"{mode} global scope splits network images and preserves focus", pages.Count == 5 &&
            current == 3 && pages[current].ImageNode == sources[2] && pages[current].PageSize.Width == 1000,
            "switching scope immediately includes both local and network wide images");
        current = ReaderPageSequence.Update(sources, pages, mode, 4, scope: WidePageSplitScope.LocalFilesOnly);
        check($"{mode} local scope restores focused network half to original", pages.Count == 4 &&
            current == 3 && pages[current] == sources[2],
            "narrowing scope preserves the current original image");

        foreach (var scope in Enum.GetValues<WidePageSplitScope>())
        {
            ReaderPageSequence.Update(sources, pages, mode, 0, splitWidePages: false, scope: scope);
            check($"{mode}/{scope} disabled switch overrides scope", pages.SequenceEqual(sources),
                "the scope selector never enables splitting by itself");
            foreach (var otherMode in new[] { ReadingMode.SinglePage, ReadingMode.VerticalScroll })
            {
                ReaderPageSequence.Update(sources, pages, otherMode, 0, scope: scope);
                check($"{mode}/{scope}/{otherMode} scope only applies to spread modes", pages.SequenceEqual(sources),
                    "single-page and scroll modes keep all originals whole");
            }
        }
    }

    private static void CheckChanges(Action<string, bool, string> check, ReadingMode mode)
    {
        var sources = Sources(2000, 1000, 2000);
        var pages = new List<RenderNode>();
        sources[0].IsSizeLoaded = false;
        ReaderPageSequence.Update(sources, pages, mode, 0);
        int current = 2;
        var focused = pages[current];
        sources[0].IsSizeLoaded = true;
        current = ReaderPageSequence.Update(sources, pages, mode, current);
        check($"{mode} late dimensions preserve the focused half", pages.Count == 5 && current == 3 && pages[current] == focused,
            "splitting an earlier image shifts page numbers without changing reading position");
        current = ReaderPageSequence.Update(sources, pages, ReadingMode.SinglePage, current);
        check($"{mode} switching to single page preserves original image", current == 2 && pages.Count == 3 && pages[current] == sources[2],
            "logical progress maps back to the original source");
        current = ReaderPageSequence.Update(sources, pages, mode, current);
        check($"{mode} switching back splits wide cover and final image", current == 3 && pages.Count == 5 &&
            pages[0].ImageNode == sources[0] && pages[1].ImageNode == sources[0] && pages[4].ImageNode == sources[2],
            "first and final images follow the same split rule");

        var half = pages[4];
        var opposite = mode == ReadingMode.SpreadLtr ? ReadingMode.SpreadRtl : ReadingMode.SpreadLtr;
        current = ReaderPageSequence.Update(sources, pages, opposite, 4);
        check($"{mode} direction switch keeps the same image half", pages[current] == half && current == 3,
            "changing reading direction reverses halves without losing the focused crop");

        var togglePages = new List<RenderNode>(pages);
        int toggleIndex = ReaderPageSequence.Update(sources, togglePages, opposite, current, splitWidePages: false);
        check($"{mode} disabling split restores whole images and focus", togglePages.SequenceEqual(sources) &&
            toggleIndex == 2 && togglePages[toggleIndex].PageSize.Width == 2000,
            "the selected half returns to its original image and page count shrinks");
        toggleIndex = ReaderPageSequence.Update(sources, togglePages, opposite, toggleIndex, splitWidePages: true);
        check($"{mode} enabling split restores half pages and focus", togglePages.Count == 5 && toggleIndex == 3 &&
            togglePages[toggleIndex].ImageNode == sources[2] && togglePages[toggleIndex].PageSize.Width == 1000,
            "the current image remains selected while logical page count grows");
        sources.Insert(0, Sources(1000)[0]);
        current = ReaderPageSequence.Update(sources, pages, opposite, current);
        check($"{mode} insertion preserves current half", pages[current] == half && current == 4,
            "source insertion uses node identity rather than a stale numeric index");

        var highResolution = Sources(1000, 2000);
        highResolution[1].Ctx.Size = new Size(2000, 2800);
        var ordinary = new List<RenderNode>();
        ReaderPageSequence.Update(highResolution, ordinary, mode, 0);
        check($"{mode} high resolution portrait remains one page", ordinary.Count == 2,
            "classification compares aspect ratios, not pixel widths");
        var allWide = Sources(2001, 2000);
        ReaderPageSequence.Update(allWide, ordinary, mode, 0);
        check($"{mode} all-wide chapter and odd image widths split cleanly", ordinary.Count == 4 &&
            ordinary[0].PageSize.Width == 1000.5 && ordinary[1].PageSize.Width == 1000.5,
            "normalized crops cover the original width without dropping a column");
    }

    private static void CheckCurl(Action<string, bool, string> check, ReadingMode mode, List<RenderNode> pages)
    {
        foreach (int direction in new[] { -1, 1 })
        {
            var state = new EngineState { CurrentMode = mode };
            Layout(state, pages, 3);
            var reader = new MangaReader(state, pages, 3);
            float dx = direction * (mode == ReadingMode.SpreadLtr ? -350 : 350);
            reader.PointerDown(1, new Vector2(1000, 700));
            reader.PointerUp(1, new Vector2(1000 + dx, 950));
            check($"{mode}/{direction} half-page gesture targets adjacent spread",
                reader.AnimationActive && reader.TargetIndex == 3 + direction * 2,
                "production pointer handling uses logical page indexes");

            var service = new PageTurnService();
            service.TryCreatePlan(new PageTurnRequest(new Vector2(dx, 250), 0, 1, 3, pages.Count, mode, state.LayoutNodes), out var plan);
            var back = pages[plan.CurlingNode!.PageIndex + direction];
            var ds = new CanvasDrawingSession();
            new Win2DPageRenderer().Draw(new PageRenderContext(ds, new Rect(-2000, -2000, 4000, 4000),
                mode, 1, 1, false, true, 0, Vector2.Zero, Vector2.Zero, plan.TargetCurl,
                plan.CurlFromRight, plan.CurlingNode, state.LayoutNodes, pages));
            var crop = back.GetSourceRect(back.Bitmap!);
            check($"{mode}/{direction} curl back samples only its half", ds.Sprites.Count > 0 && ds.Sprites.All(s =>
                s.Page == (int)back.Source && s.Source.X >= crop.X - 0.01 &&
                s.Source.X + s.Source.Width <= crop.X + crop.Width + 0.01),
                "the flipped paper never stretches the full wide image onto a half page");

            var diagonal = new CanvasDrawingSession();
            reader.Draw(diagonal);
            var front = plan.CurlingNode;
            bool cropsValid = diagonal.Polygons.Count > 0 && diagonal.Polygons.All(p =>
            {
                if (!Matrix3x2.Invert(p.Texture, out var inverse)) return false;
                return new[] { front, back }.Where(n => (int)n.Source == p.Page).Any(n =>
                {
                    var source = n.GetSourceRect(n.Bitmap!);
                    return p.Polygon.Select(v => Vector2.Transform(v, inverse)).All(v =>
                        v.X >= source.X - 0.1 && v.X <= source.X + source.Width + 0.1 &&
                        v.Y >= source.Y - 0.1 && v.Y <= source.Y + source.Height + 0.1);
                });
            });
            check($"{mode}/{direction} diagonal curl stays within half-page texture", cropsValid,
                "inverse texture mapping of each polygon remains inside its assigned crop");
            reader.FinishAnimation();
            check($"{mode}/{direction} half-page gesture finishes", reader.CurrentPageIndex == 3 + direction * 2 && !reader.AnimationActive,
                "animation completion lands on the requested logical spread");
        }
    }

    private static void CheckImageWindow(Action<string, bool, string> check, ReadingMode mode)
    {
        var sources = Sources(1000, 2000, 1000);
        var pages = new List<RenderNode>();
        ReaderPageSequence.Update(sources, pages, mode, 0);
        var state = new EngineState { CurrentMode = mode };
        Layout(state, pages, 1);
        var bitmap = sources[1].Bitmap!;
        var window = new ReaderImageWindowLoadController();
        window.UpdateWindow([pages[1]], pages, pages[1].Bounds, 0, new CanvasDevice(), _ => false);
        check($"{mode} visible half protects shared bitmap", pages[1].Bitmap == bitmap && pages[2].Bitmap == bitmap && !bitmap.IsDisposed,
            "offscreen sibling cannot evict the image used by the visible half");
        sources[1].Bitmap = null;
        sources[1].ImageStrategy = new NoopStrategy();
        var tickets = new List<ReaderImageLoadTicket>();
        window.UpdateWindow(state.LayoutNodes, pages, new Rect(-2000, -2000, 4000, 4000), 0, new CanvasDevice(), ticket =>
        {
            tickets.Add(ticket);
            return true;
        });
        check($"{mode} two halves enqueue one original image", tickets.Count == 1 && tickets[0].Node == sources[1],
            "decoding, retry and cancellation operate on the shared resource owner");
        window.Reset();
    }

    private sealed class LocalStrategy : NoopStrategy, IImageSourceStrategy
    {
        public bool IsLocalSource => true;
    }

    private class NoopStrategy : IImageSourceStrategy
    {
        public bool CanHandle(object source) => true;
        public Task InitImageAsync(ImageLoadingContext ctx) => Task.CompletedTask;
        public Task PreloadImageAsync(ImageLoadingContext ctx) => Task.CompletedTask;
    }
}
