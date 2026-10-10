using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using ShadowViewer.Plugin.Local.Readers.Internal;
using Windows.Foundation;

internal static class PageIndexPreparationChecks
{
    public static async Task RunAsync(Action<string, bool, string> check)
    {
        await CheckOptionalPreparationAsync(check);
        // 整章大于生产尺寸通道的容量；慢探测期间不能发布部分页数。
        const int sourceCount = 1200;
        var sources = Enumerable.Range(0, sourceCount).Select(i => new RenderNode
        {
            Source = i,
            Ctx = new ImageLoadingContext { Source = i, Size = new Size(200, 300) }
        }).ToList();
        var preparation = new ReaderPageIndexPreparation();
        preparation.Add(sources);
        var processed = new ConcurrentDictionary<RenderNode, int>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReaderBackgroundPipeline<RenderNode>? pipeline = null;
        pipeline = new ReaderBackgroundPipeline<RenderNode>(512, 2, false, false, async (request, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            var node = request.Payload;
            try
            {
                processed.AddOrUpdate(node, 1, (_, count) => count + 1);
                int sourceIndex = (int)node.Source;
                if (sourceIndex == 7) throw new InvalidDataException("Unreadable image");
                if (sourceIndex == 8) return; // 无本地缓存的网络源允许暂时没有尺寸。
                node.Ctx.Size = new Size(sourceIndex % 10 == 0 ? 2000 : 1000, 1400);
                node.IsSizeLoaded = true;
            }
            catch (InvalidDataException) { }
            finally
            {
                preparation.Complete(node);
                preparation.Dispatch(pipeline!.TryEnqueue);
                if (preparation.IsReady) finished.TrySetResult();
            }
        });
        pipeline.Start();
        try
        {
            preparation.Dispatch(pipeline.TryEnqueue);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            check("Page index waits for all source probes", !preparation.IsReady && processed.IsEmpty,
                "a queued chapter is not ready while size probes remain blocked");
            int savedSourceIndex = 987; // 阅读进度在尺寸请求已入队后才返回。
            release.SetResult();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            check("Page index drains a chapter beyond queue capacity", preparation.IsReady &&
                processed.Count == sourceCount && processed.Values.All(count => count == 1),
                "all 1200 sources are probed exactly once despite the 512-request limit");
            check("Failed or unknown dimensions do not block opening", !sources[7].IsSizeLoaded &&
                !sources[8].IsSizeLoaded && preparation.IsReady,
                "failed and uncached sources settle as whole-image placeholders");

            foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
            {
                var pages = new List<RenderNode>();
                ReaderPageSequence.Update(sources, pages, mode, 0);
                int restored = pages.FindIndex(n => n.ImageNode == sources[savedSourceIndex]);
                var state = new EngineState { CurrentMode = mode };
                new ReaderLayoutService().UpdateActiveLayout(state, pages, restored, 0, false,
                    new Vector2(2000, 1400), new ReaderLayoutCacheState());
                check($"{mode} publishes final total with saved source position", pages.Count == 1320 &&
                    restored == 1086 && state.LayoutNodes.Any(n => n.ImageNode == sources[savedSourceIndex]),
                    "the first published spread contains the saved original image after every prior split is known");
                check($"{mode} UI synchronization preserves restored page within spread",
                    ReaderUiSyncService.ResolvePageIndex(mode, state.LayoutNodes, restored, restored - 1) == restored,
                    "a neighboring page cannot replace the saved page merely by being equally close to the camera");
                check("Vertical UI synchronization still follows camera position",
                    ReaderUiSyncService.ResolvePageIndex(ReadingMode.VerticalScroll, state.LayoutNodes, restored, restored - 1) == restored - 1,
                    "scrolling continues to update progress from the visible content");
            }
        }
        finally
        {
            release.TrySetResult();
            pipeline.Stop();
        }

        var previous = sources[7];
        var next = new RenderNode { Ctx = new ImageLoadingContext() };
        preparation.Add([previous]);
        preparation.Reset();
        preparation.Add([next]);
        preparation.Complete(previous);
        check("Chapter change rejects old probe completion", !preparation.IsReady,
            "completing a retired chapter cannot mark its replacement ready");
        var dispatched = new List<RenderNode>();
        preparation.Dispatch(node => { dispatched.Add(node); return true; });
        check("Chapter change discards queued old sources", dispatched.SequenceEqual(new[] { next }),
            "only replacement sources remain queued");
        preparation.Complete(next);
        check("Replacement chapter can complete", preparation.IsReady, "no outstanding count leaks across resets");

        foreach (var mode in Enum.GetValues<ReadingMode>())
        {
            var node = new RenderNode
            {
                PageIndex = 0,
                Ctx = new ImageLoadingContext { Size = new Size(1000, 1400) },
                Bounds = new Rect(-500, -700, 1000, 1400),
                IsSizeLoaded = true,
                Bitmap = new CanvasBitmap(0, new Size(1000, 1400))
            };
            var state = new EngineState { CurrentMode = mode, LayoutNodes = [node] };
            var reader = new MangaReader(state, [node], 0);
            int chapters = 0;
            reader.ChapterNavigationRequested += (_, _) => chapters++;
            reader.SetPreparingPages(true);
            reader.TurnPage(1);
            reader.Wheel(-120);
            reader.PointerDown(1, new Vector2(1000, 700));
            reader.PointerUp(1, new Vector2(600, 700));
            var frame = new CanvasDrawingSession();
            reader.Draw(frame);
            check($"{mode} preparing index blocks navigation and old frames", chapters == 0 &&
                reader.CurrentPageIndex == 0 && !reader.AnimationActive && frame.Images.Count == 0,
                "commands, wheel, gestures and drawing cannot expose provisional page state");
            reader.SetPreparingPages(false);
            reader.Draw(frame);
            reader.TurnPage(1);
            check($"{mode} ready index restores navigation and rendering", chapters == 1 && frame.Images.Count == 1,
                "the preparation gate releases normally after publication");
        }
    }
    private static async Task CheckOptionalPreparationAsync(Action<string, bool, string> check)
    {
        foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
        {
            var preparation = new ReaderPageIndexPreparation();
            var strategy = new DeferredSizeStrategy();
            var sources = Enumerable.Range(0, 30).Select(i => new RenderNode
            {
                Source = i,
                Ctx = new ImageLoadingContext { Source = i, Size = new Size(200, 300) },
                ImageStrategy = strategy
            }).ToList();
            preparation.Add(sources.Take(15), buildPageIndex: false);
            preparation.Add(sources.Skip(15), buildPageIndex: true);
            var probes = new List<RenderNode>();
            preparation.Dispatch(node => { probes.Add(node); return true; });
            check($"{mode} disabled index skips whole-chapter probes", preparation.IsReady && probes.Count == 0,
                "all batches use the setting captured at opening, even if it changes mid-chapter");

            var pages = new List<RenderNode>();
            ReaderPageSequence.Update(sources, pages, mode, 0);
            var target = sources[20];
            int current = pages.FindIndex(n => n.ImageNode == target);
            check($"{mode} restores progress without prepared dimensions", current == 20 &&
                pages.Count == 30 && strategy.Initializations == 0,
                "whole-image placeholders restore the saved original without scanning");
            target.Bounds = new Rect(0, 0, 200, 300);
            var window = new ReaderImageWindowLoadController();
            var tickets = new List<ReaderImageLoadTicket>();
            var errors = new List<Exception>();
            int updates = 0;
            var service = new ReaderImageLoadService(window,
                (ctx, _, _) => Task.FromResult<CanvasBitmap?>(new CanvasBitmap((int)ctx.Source, ctx.Size)),
                _ => updates++, errors.Add);
            try
            {
                window.UpdateWindow([target], pages, target.Bounds, 0, new CanvasDevice(), ticket =>
                {
                    tickets.Add(ticket);
                    return true;
                });
                foreach (var ticket in tickets) await service.LoadAsync(ticket, CancellationToken.None);
                check($"{mode} disabled index still loads visible images on demand", target.IsLoaded &&
                    target.IsSizeLoaded && strategy.Initializations == 1 && updates == 1 && errors.Count == 0 &&
                    sources.Where(n => n != target).All(n => !n.IsSizeLoaded),
                    "offscreen originals stay unprobed while the requested original loads normally");
                current = ReaderPageSequence.Update(sources, pages, mode, current);
                check($"{mode} deferred dimensions split wide pages and keep focus", pages.Count == 31 &&
                    pages[current].ImageNode == target && pages[current].PageSize.Width == 1000,
                    "splitting remains independent of index preparation");
                current = ReaderPageSequence.Update(sources, pages, mode, current, splitWidePages: false);
                current = ReaderPageSequence.Update(sources, pages, mode, current, splitWidePages: true);
                check($"{mode} split toggles reuse known dimensions", strategy.Initializations == 1 &&
                    pages.Count == 31 && pages[current].ImageNode == target && preparation.IsReady,
                    "rebuilding page numbers does not restart full-chapter probing");

                preparation.Reset();
                preparation.Add(sources.Take(15), buildPageIndex: true);
                preparation.Add(sources.Skip(15), buildPageIndex: false);
                preparation.Dispatch(node => { probes.Add(node); return true; });
                check($"{mode} next open enables full index preparation", !preparation.IsReady && probes.Count == 29,
                    "all unknown sizes are probed; already loaded dimensions are reused");
                preparation.Reset();
                preparation.Add(sources, buildPageIndex: false);
                preparation.Complete(probes[0]);
                probes.Clear();
                preparation.Dispatch(node => { probes.Add(node); return true; });
                check($"{mode} next open disables indexing and clears old probes", preparation.IsReady && probes.Count == 0,
                    "stale completions cannot block an unindexed chapter");
            }
            finally
            {
                window.Reset();
                foreach (var node in sources) node.Retire();
            }
        }
    }

    private sealed class DeferredSizeStrategy : IImageSourceStrategy
    {
        public int Initializations;
        public bool CanHandle(object source) => true;
        public Task InitImageAsync(ImageLoadingContext ctx)
        {
            Initializations++;
            ctx.Size = new Size(2000, 1400);
            return Task.CompletedTask;
        }
        public Task PreloadImageAsync(ImageLoadingContext ctx) => Task.CompletedTask;
    }
}
