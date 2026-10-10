using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using ShadowViewer.Plugin.Local.Readers.Internal;
using Windows.Foundation;

internal static class ImageLoadingChecks
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly CanvasDevice Device = new();
    private static readonly Rect View = new(0, 0, 200, 300);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static RenderNode Node(int index, TestStrategy? strategy = null) => new()
    {
        PageIndex = index, Bounds = View, Source = index,
        Ctx = new ImageLoadingContext { Source = index, Size = new Size(200, 300) },
        ImageStrategy = strategy ?? new TestStrategy()
    };

    public static async Task RunAsync(Action<string, bool, string> check)
    {
        await WindowCancellationDoesNotBlockAsync(check);
        await WindowCancellationReentrancyAsync(check);
        await RapidWindowChangesAsync(check);
        await SupersededDecodeAsync(check);
        await LargeJumpReleasesImagesOffThreadAsync(check);
        await WindowAndLoadingAsync(check);
        await PipelineAsync(check);
        await DiskCacheAsync(check);
    }

    private static async Task LargeJumpReleasesImagesOffThreadAsync(Action<string, bool, string> check)
    {
        var nodes = Enumerable.Range(0, 200).Select(i => Node(i)).ToList();
        for (int i = 0; i < nodes.Count; i++) nodes[i].Bounds = new Rect(0, i * View.Height, View.Width, View.Height);
        var window = new ReaderImageWindowLoadController();
        var requests = new List<ReaderImageLoadTicket>();
        bool Enqueue(ReaderImageLoadTicket ticket) { requests.Add(ticket); return true; }
        window.UpdateWindow(nodes, nodes, nodes[185].Bounds, 4, Device, Enqueue);
        var releaseStarted = Signal();
        var allowRelease = Signal();
        var oldBitmap = new CanvasBitmap(185, new Size(200, 300))
        {
            Disposing = () =>
            {
                releaseStarted.TrySetResult();
                allowRelease.Task.GetAwaiter().GetResult();
            }
        };
        nodes[185].SetBitmap(oldBitmap);
        requests.Clear();
        var jump = Task.Run(() => window.UpdateWindow(nodes, nodes, nodes[33].Bounds, 4, Device, Enqueue));
        try
        {
            await releaseStarted.Task.WaitAsync(Timeout);
            bool returned = false;
            try { await jump.WaitAsync(TimeSpan.FromSeconds(1)); returned = true; }
            catch (TimeoutException) { }
            check("Jump from page 186 to 34 does not wait for native bitmap release", returned,
                "slow GPU cleanup must not block rendering or enqueueing the destination page");
            if (returned)
            {
                check("Large jump enqueues page 34 before releasing page 186",
                    requests.Count == 9 && requests[0].Node == nodes[33] && !oldBitmap.IsDisposed && !nodes[185].IsLoaded,
                    "old image ownership is detached immediately while native cleanup runs independently");
                window.UpdateWindow(nodes, nodes, nodes[185].Bounds, 0, Device, Enqueue);
                var replacement = new CanvasBitmap(185, new Size(200, 300));
                nodes[185].SetBitmap(replacement);
                allowRelease.TrySetResult();
                for (int i = 0; i < 100 && !oldBitmap.IsDisposed; i++) await Task.Delay(10);
                check("Delayed release cannot dispose a reloaded page",
                    oldBitmap.IsDisposed && nodes[185].Bitmap == replacement && !replacement.IsDisposed,
                    "cleanup owns the detached bitmap rather than the mutable page node");
            }
        }
        finally
        {
            allowRelease.TrySetResult();
            await jump.WaitAsync(Timeout);
            window.Reset();
            foreach (var node in nodes) node.Retire();
        }
    }

    private static async Task SupersededDecodeAsync(Action<string, bool, string> check)
    {
        var nodes = new[] { Node(0), Node(1) };
        var window = new ReaderImageWindowLoadController();
        var oldDecode = new TaskCompletionSource<CanvasBitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodeStarted = Signal();
        var finalLoaded = Signal();
        var workerReleased = Signal();
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var service = new ReaderImageLoadService(window, (context, _, _) =>
        {
            if ((int)context.Source == 0)
            {
                decodeStarted.TrySetResult();
                return oldDecode.Task;
            }
            return Task.FromResult<CanvasBitmap?>(new CanvasBitmap(1, new Size(200, 300)));
        }, node => { if (node == nodes[1]) finalLoaded.TrySetResult(); }, errors.Enqueue);
        var pipeline = new ReaderBackgroundPipeline<ReaderImageLoadTicket>(4, 1, false, false, async (request, token) =>
        {
            await service.LoadAsync(request.Payload, token);
            if (request.Payload.Node == nodes[0]) workerReleased.TrySetResult();
        });
        pipeline.Start();
        var lateBitmap = new CanvasBitmap(0, new Size(200, 300));
        try
        {
            window.UpdateWindow([nodes[0]], nodes, View, 0, Device, pipeline.TryEnqueue);
            await decodeStarted.Task.WaitAsync(Timeout);
            window.UpdateWindow([nodes[1]], nodes, View, 0, Device, pipeline.TryEnqueue);
            bool progressed = false;
            try { await finalLoaded.Task.WaitAsync(TimeSpan.FromSeconds(1)); progressed = true; }
            catch (TimeoutException) { }
            check("Superseded decode releases the worker for the final page", progressed && nodes[1].IsLoaded,
                "an old decoder that ignores cancellation cannot block the latest visible image");
            oldDecode.TrySetResult(lateBitmap);
            await workerReleased.Task.WaitAsync(Timeout);
            await finalLoaded.Task.WaitAsync(Timeout);
            for (int i = 0; i < 100 && !lateBitmap.IsDisposed; i++) await Task.Delay(10);
            check("Superseded decoder disposes its late bitmap", lateBitmap.IsDisposed && !nodes[0].IsLoaded && errors.IsEmpty,
                "abandoned results are observed and cannot leak or replace the current image");
        }
        finally
        {
            oldDecode.TrySetResult(lateBitmap);
            window.Reset();
            pipeline.Stop();
            foreach (var node in nodes) node.Retire();
        }
    }

    private static async Task RapidWindowChangesAsync(Action<string, bool, string> check)
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var nodes = Enumerable.Range(0, 1000).Select(i => Node(i)).ToList();
        for (int i = 0; i < nodes.Count; i++) nodes[i].Bounds = new Rect(0, i * View.Height, View.Width, View.Height);
        var window = new ReaderImageWindowLoadController(() => now, TimeSpan.FromMilliseconds(80));
        var requests = new List<ReaderImageLoadTicket>();
        var errors = new List<Exception>();
        bool Enqueue(ReaderImageLoadTicket ticket) { requests.Add(ticket); return true; }
        foreach (int index in Enumerable.Range(0, 100).Select(i => i * 9))
        {
            window.UpdateWindow(nodes, nodes, nodes[index].Bounds, 1, Device, Enqueue);
            now += TimeSpan.FromMilliseconds(10);
        }
        check("Rapid vertical seeking does not enqueue intermediate pages", requests.Count == 0,
            "100 viewport changes keep the bitmap workers available for the final position");
        now += TimeSpan.FromMilliseconds(80);
        window.UpdateWindow(nodes, nodes, nodes[891].Bounds, 1, Device, Enqueue);
        check("Settled seek loads the final visible page before neighbors",
            requests.Select(t => t.Node.PageIndex).SequenceEqual([891, 890, 892]),
            "settling requires no further pointer event");
        var service = new ReaderImageLoadService(window, (_, _, _) =>
            Task.FromResult<CanvasBitmap?>(new CanvasBitmap(891, new Size(200, 300))), _ => { }, errors.Add);
        await service.LoadAsync(requests[0], CancellationToken.None);
        check("Final seek image is published successfully", nodes[891].IsLoaded && errors.Count == 0,
            "the final page can replace its placeholder after scrubbing");
        requests.Clear();
        for (int i = 0; i < 20; i++)
        {
            now += TimeSpan.FromMilliseconds(10);
            var viewport = nodes[891].Bounds;
            viewport.Y += i / 10.0;
            viewport.Height -= 2;
            window.UpdateWindow(nodes, nodes, viewport, 1, Device, Enqueue);
        }
        check("Movement within the same visible page does not restart loading", requests.Count == 0 && nodes[891].IsLoaded,
            "settling follows visible page identity rather than every pixel of camera motion");
        window.Reset();
        foreach (var node in nodes) node.Retire();
    }

    private static async Task WindowCancellationDoesNotBlockAsync(Action<string, bool, string> check)
    {
        var nodes = new[] { Node(0), Node(1) };
        var requests = new List<ReaderImageLoadTicket>();
        var window = new ReaderImageWindowLoadController();
        bool Enqueue(ReaderImageLoadTicket ticket) { requests.Add(ticket); return true; }
        window.UpdateWindow([nodes[0]], nodes, View, 0, Device, Enqueue);
        var previous = requests[0];
        var callbackStarted = Signal();
        var releaseCallback = Signal();
        var callbackFinished = Signal();
        using var registration = previous.CancellationToken.Register(() =>
        {
            callbackStarted.TrySetResult();
            releaseCallback.Task.GetAwaiter().GetResult();
            callbackFinished.TrySetResult();
        });
        var update = Task.Run(() => window.UpdateWindow([nodes[1]], nodes, View, 0, Device, Enqueue));
        bool returned = false;
        try
        {
            await callbackStarted.Task.WaitAsync(Timeout);
            try { await update.WaitAsync(TimeSpan.FromSeconds(1)); returned = true; }
            catch (TimeoutException) { }
            check("Window update never waits for slow cancellation callbacks", returned,
                "the render thread must remain available while old I/O unwinds");
            check("Eviction marks old tickets canceled before callbacks finish",
                previous.CancellationToken.IsCancellationRequested && !window.IsWanted(previous),
                "late decoding cannot publish into the new viewport");
        }
        finally
        {
            releaseCallback.TrySetResult();
            await Task.WhenAll(update, callbackFinished.Task).WaitAsync(Timeout);
            window.Reset();
        }
    }

    private static async Task WindowCancellationReentrancyAsync(Action<string, bool, string> check)
    {
        foreach (bool reset in new[] { false, true })
        {
            var nodes = Enumerable.Range(0, 4).Select(i => Node(i)).ToList();
            for (int i = 0; i < nodes.Count; i++) nodes[i].Bounds = new Rect(0, i * View.Height, View.Width, View.Height);
            var window = new ReaderImageWindowLoadController();
            var requests = new List<ReaderImageLoadTicket>();
            bool Enqueue(ReaderImageLoadTicket ticket) { requests.Add(ticket); return true; }
            window.UpdateWindow(nodes, nodes, nodes[1].Bounds, 1, Device, Enqueue);
            var previous = requests.ToArray();
            int callbacks = 0;
            var callbackFinished = Signal();
            using var registration = previous[0].CancellationToken.Register(() =>
            {
                callbacks++;
                // Cancellation continuations may complete other requests in the same batch.
                foreach (var ticket in previous) window.Complete(ticket, false);
                callbackFinished.TrySetResult();
            });
            Exception? failure = null;
            try
            {
                if (reset) window.Reset();
                else window.UpdateWindow(nodes, nodes, nodes[3].Bounds, 0, Device, Enqueue);
            }
            catch (Exception ex) { failure = ex; }
            await callbackFinished.Task.WaitAsync(Timeout);
            string operation = reset ? "Reset" : "Vertical window jump";
            check($"{operation} tolerates synchronous completion during cancellation", failure == null && callbacks == 1,
                failure?.ToString() ?? "cancellation reenters completion without invalidating the eviction batch");
            check($"{operation} cancels every detached request",
                previous.All(t => t.CancellationToken.IsCancellationRequested && !window.IsWanted(t)),
                "completing another ticket must not skip its cancellation");

            requests.Clear();
            window.UpdateWindow(nodes, nodes, nodes[1].Bounds, 1, Device, Enqueue);
            foreach (var ticket in previous) window.Complete(ticket, false);
            check($"{operation} allows immediate return to previous pages",
                requests.Select(t => t.Node.PageIndex).SequenceEqual([1, 0, 2]) && requests.All(window.IsWanted),
                "late completions cannot remove replacement requests or delay them with retry backoff");
            window.Reset();
        }
    }

    private static async Task WindowAndLoadingAsync(Action<string, bool, string> check)
    {
        var nodes = Enumerable.Range(0, 8).Select(i => Node(i)).ToList();
        var requests = new List<ReaderImageLoadTicket>();
        var window = new ReaderImageWindowLoadController();
        bool Enqueue(ReaderImageLoadTicket ticket) { requests.Add(ticket); return true; }
        window.UpdateWindow([nodes[3]], nodes, View, 1, Device, Enqueue);
        window.UpdateWindow([nodes[3]], nodes, View, 1, Device, Enqueue);
        check("Image window loads visible page before neighbors once", requests.Select(t => t.Node.PageIndex).SequenceEqual([3, 2, 4]),
            "visible page gets priority; second frame cannot duplicate in-flight requests");
        var old = requests.Single(t => t.Node == nodes[3]);
        nodes[3].PageIndex = 99;
        window.Complete(old, true);
        window.UpdateWindow([nodes[3]], nodes, View, 1, Device, Enqueue);
        var replacement = requests[^1];
        check("Image requests survive page-index changes", replacement.Node == nodes[3] && window.IsWanted(replacement),
            "node identity remains stable after insertion/reindexing");
        window.Reset();
        check("Window reset cancels all outstanding requests", requests.All(t => t.CancellationToken.IsCancellationRequested || t == old),
            "old chapter work is canceled");
        window.UpdateWindow([nodes[3]], nodes, View, 0, Device, Enqueue);
        var current = requests[^1];
        window.Complete(replacement, false);
        check("Old completion cannot remove a replacement request", window.IsWanted(current), "ticket identity is checked");
        nodes[0].Ctx.Bytes = [1, 2, 3];
        window.UpdateWindow([], nodes, View, 0, Device, Enqueue);
        check("Offscreen initialization bytes are released without a bitmap", nodes[0].Ctx.Bytes == null && current.CancellationToken.IsCancellationRequested,
            "eviction releases byte buffers and cancels downloads");

        var rejectedNode = Node(0);
        var rejectedWindow = new ReaderImageWindowLoadController();
        rejectedWindow.UpdateWindow([rejectedNode], [rejectedNode], View, 0, Device, _ => false);
        ReaderImageLoadTicket? accepted = null;
        rejectedWindow.UpdateWindow([rejectedNode], [rejectedNode], View, 0, Device, t => { accepted = t; return true; });
        check("Full queue rejection can retry next frame", accepted != null && rejectedWindow.IsWanted(accepted),
            "rejection does not leave a permanent loading marker");
        rejectedWindow.Reset();

        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var retryWindow = new ReaderImageWindowLoadController(() => now);
        var retryNode = Node(0);
        var retryTickets = new List<ReaderImageLoadTicket>();
        void UpdateRetry() => retryWindow.UpdateWindow([retryNode], [retryNode], View, 0, Device, t => { retryTickets.Add(t); return true; });
        UpdateRetry(); retryWindow.Complete(retryTickets[^1], false); UpdateRetry();
        check("Image failures back off instead of retrying every frame", retryTickets.Count == 1, "first retry waits 500ms");
        now += TimeSpan.FromMilliseconds(500); UpdateRetry();
        check("Image failures become eligible for retry", retryTickets.Count == 2, "retry resumes after backoff");
        retryWindow.Reset();

        var strategy = new TestStrategy
        {
            Init = context => { context.CachedFilePath = "cached.img"; return Task.CompletedTask; },
            Preload = context => { context.Bytes = [42]; context.Size = new Size(1000, 1400); return Task.CompletedTask; }
        };
        var hookNode = Node(0, strategy);
        var hookWindow = new ReaderImageWindowLoadController();
        ReaderImageLoadTicket? hookTicket = null;
        var errors = new List<Exception>();
        int refreshed = 0;
        bool sawHook = false;
        var service = new ReaderImageLoadService(hookWindow, (context, _, _) =>
        {
            sawHook = context.Bytes is [42] && context.CachedFilePath == "cached.img";
            return Task.FromResult<CanvasBitmap?>(new CanvasBitmap(0, new Size(1000, 1400)));
        }, _ => refreshed++, errors.Add);
        await service.InitializeAsync(hookNode, CancellationToken.None);
        check("Cache path alone is not proof of real dimensions", !hookNode.IsSizeLoaded, "placeholder size is not marked loaded");
        void UpdateHook() => hookWindow.UpdateWindow([hookNode], [hookNode], View, 0, Device, t => { hookTicket = t; return true; });
        UpdateHook(); await service.LoadAsync(hookTicket!, CancellationToken.None);
        check("Preload Hook data and late dimensions reach decoder", sawHook && hookNode.IsLoaded && hookNode.IsSizeLoaded && refreshed == 1,
            "actual dimensions are published only after successful loading");
        hookWindow.UpdateWindow([], [hookNode], View, 0, Device, _ => true);
        check("Eviction releases Hook bytes and preload state", hookNode.Ctx.Bytes == null && !hookNode.Preloaded && !hookNode.IsLoaded,
            "reentering the window runs the Hook again");
        UpdateHook(); await service.LoadAsync(hookTicket!, CancellationToken.None);
        check("Evicted images reload their Hook payload", hookNode.IsLoaded && strategy.PreloadCalls == 2 && strategy.InitCalls == 2,
            "byte-based strategies can recreate their data");
        hookWindow.Reset(); hookNode.Retire();
        var late = new CanvasBitmap(0, new Size(1, 1)); hookNode.SetBitmap(late);
        check("Retired nodes reject and dispose late bitmaps", late.IsDisposed && !hookNode.IsLoaded, "retirement is permanent");

        var decodeStarted = Signal(); var decodeFinish = new TaskCompletionSource<CanvasBitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceledNode = Node(0);
        var canceledWindow = new ReaderImageWindowLoadController();
        ReaderImageLoadTicket? canceledTicket = null;
        canceledWindow.UpdateWindow([canceledNode], [canceledNode], View, 0, Device, t => { canceledTicket = t; return true; });
        var canceledService = new ReaderImageLoadService(canceledWindow, async (_, _, _) =>
        { decodeStarted.TrySetResult(); return await decodeFinish.Task; }, _ => { }, errors.Add);
        var loading = canceledService.LoadAsync(canceledTicket!, CancellationToken.None);
        await decodeStarted.Task.WaitAsync(Timeout);
        canceledWindow.UpdateWindow([], [canceledNode], View, 0, Device, _ => true);
        var abandonedBitmap = new CanvasBitmap(0, new Size(1000, 1400));
        decodeFinish.TrySetResult(abandonedBitmap);
        await loading.WaitAsync(Timeout);
        // 取消后消费者先退出；无法取消的原生解码由独立清理任务观察并释放。
        for (int i = 0; i < 100 && !abandonedBitmap.IsDisposed; i++) await Task.Delay(10);
        check("Canceled GPU decode disposes its unpublishable bitmap", abandonedBitmap.IsDisposed && !canceledNode.IsLoaded && canceledNode.Ctx.Bytes == null,
            "decoder completion after eviction cannot resurrect a page");

        var preloadStarted = Signal(); var preloadFinish = Signal(); var hookFinished = Signal();
        var staleStrategy = new TestStrategy { Preload = async context =>
        { preloadStarted.TrySetResult(); await preloadFinish.Task; context.Bytes = [77]; context.Size = new Size(900, 1200); hookFinished.TrySetResult(); } };
        var staleNode = Node(0, staleStrategy);
        var staleWindow = new ReaderImageWindowLoadController(); ReaderImageLoadTicket? staleTicket = null;
        staleWindow.UpdateWindow([staleNode], [staleNode], View, 0, Device, t => { staleTicket = t; return true; });
        var staleService = new ReaderImageLoadService(staleWindow, (_, _, _) => Task.FromResult<CanvasBitmap?>(new CanvasBitmap(0, new Size(900, 1200))),
            _ => { }, errors.Add);
        var staleLoad = staleService.LoadAsync(staleTicket!, CancellationToken.None);
        await preloadStarted.Task.WaitAsync(Timeout); staleWindow.Reset(); staleNode.Retire();
        await staleLoad.WaitAsync(Timeout);
        check("Canceled legacy Hook does not retain a pipeline worker", staleLoad.IsCompleted && !preloadFinish.Task.IsCompleted,
            "isolated context permits canceling the wait even if a Hook ignores the token");
        preloadFinish.TrySetResult(); await hookFinished.Task.WaitAsync(Timeout);
        check("Uncancelable legacy Hook cannot mutate retired context", staleNode.Ctx.Bytes == null && !staleNode.IsSizeLoaded && !staleNode.IsLoaded,
            "Hook writes stay in the request's isolated context");

        var failingStrategy = new TestStrategy { Preload = _ => throw new IOException("temporary failure") };
        var failingNode = Node(0, failingStrategy); now = DateTimeOffset.UnixEpoch;
        var failingWindow = new ReaderImageWindowLoadController(() => now); ReaderImageLoadTicket? failingTicket = null;
        void UpdateFailing() => failingWindow.UpdateWindow([failingNode], [failingNode], View, 0, Device, t => { failingTicket = t; return true; });
        var failingService = new ReaderImageLoadService(failingWindow, (_, _, _) => Task.FromResult<CanvasBitmap?>(new CanvasBitmap(0, new Size(1000, 1400))),
            _ => { }, errors.Add);
        UpdateFailing(); await failingService.LoadAsync(failingTicket!, CancellationToken.None);
        failingStrategy.Preload = context => { context.Bytes = [1]; return Task.CompletedTask; };
        now += TimeSpan.FromSeconds(1); UpdateFailing(); await failingService.LoadAsync(failingTicket!, CancellationToken.None);
        check("Failed preload does not poison later retries", failingNode.IsLoaded && failingNode.Preloaded && failingStrategy.PreloadCalls == 2,
            "success state is published only after bitmap decoding succeeds");
        failingWindow.Reset(); failingNode.Retire();

        var initStarted = Signal(); var initFinish = Signal();
        var serialStrategy = new TestStrategy { Init = async context =>
        { initStarted.TrySetResult(); await initFinish.Task; context.Size = new Size(1000, 1400); } };
        var serialNode = Node(0, serialStrategy); var serialWindow = new ReaderImageWindowLoadController();
        ReaderImageLoadTicket? serialTicket = null;
        serialWindow.UpdateWindow([serialNode], [serialNode], View, 0, Device, t => { serialTicket = t; return true; });
        var serialService = new ReaderImageLoadService(serialWindow, (_, _, _) => Task.FromResult<CanvasBitmap?>(new CanvasBitmap(0, new Size(1000, 1400))),
            _ => { }, errors.Add);
        var initializing = serialService.InitializeAsync(serialNode, CancellationToken.None);
        await initStarted.Task.WaitAsync(Timeout);
        var bitmapLoad = serialService.LoadAsync(serialTicket!, CancellationToken.None);
        initFinish.TrySetResult(); await Task.WhenAll(initializing, bitmapLoad).WaitAsync(Timeout);
        check("Size initialization and bitmap loading serialize per node", serialStrategy.InitCalls == 1 && serialStrategy.PreloadCalls == 1 && serialNode.IsLoaded,
            "two pipelines cannot race the same strategy context");
        serialWindow.Reset(); serialNode.Retire();

        var first = new CanvasBitmap(0, new Size(1, 1)); var second = new CanvasBitmap(0, new Size(1, 1));
        var owned = Node(0); owned.SetBitmap(first); owned.SetBitmap(second);
        check("Replacing a bitmap releases the previous GPU resource", first.IsDisposed && !second.IsDisposed, "ownership is transferred exactly once");
        owned.Retire();

        var cache = new ReaderLayoutCacheState(); var layoutNode = Node(0);
        layoutNode.IsSizeLoaded = true; layoutNode.Ctx.Size = new Size(1000, 1400);
        var layout = new ReaderLayoutService(); var state = new EngineState();
        layout.UpdateActiveLayout(state, [layoutNode], 0, 0, false, new(500, 700), cache);
        layoutNode.Ctx.Size = new Size(2000, 2800); cache.IsDirty = true;
        layout.UpdateActiveLayout(state, [layoutNode], 0, 0, false, new(500, 700), cache);
        check("Late image dimensions invalidate cached scale", Math.Abs(cache.CachedScale - 0.25f) < 0.001f && layoutNode.Bounds.Width == 500,
            "unchanged viewport and page count still recalculate dimensions");
        cache.ResetAfterClearItems();
        check("Chapter change clears sampled dimensions", cache.ModeWidth == 0 && cache.ModeHeight == 0 && cache.IsDirty,
            "old chapter dimensions cannot affect the next chapter");
    }

    private static async Task PipelineAsync(Action<string, bool, string> check)
    {
        var processed = new List<int>(); var drained = Signal();
        var pipeline = new ReaderBackgroundPipeline<int>(2, 1, true, false, (request, _) =>
        {
            lock (processed) { processed.Add(request.Payload); if (processed.Count == 2) drained.TrySetResult(); }
            return Task.CompletedTask;
        });
        bool first = pipeline.TryEnqueue(1), second = pipeline.TryEnqueue(2), rejected = !pipeline.TryEnqueue(3);
        pipeline.Start(); await drained.Task.WaitAsync(Timeout); pipeline.Stop();
        check("Bounded pipeline refuses overflow without dropping earlier work", first && second && rejected && processed.SequenceEqual([1, 2]),
            "callers can safely retry rejected requests");

        var survived = Signal(); int failures = 0;
        var resilient = new ReaderBackgroundPipeline<int>(4, 1, true, false, (request, _) =>
        {
            if (request.Payload == 1) throw new IOException("failed request");
            survived.TrySetResult(); return Task.CompletedTask;
        }, onError: _ => Interlocked.Increment(ref failures));
        resilient.TryEnqueue(1); resilient.TryEnqueue(2); resilient.Start();
        await survived.Task.WaitAsync(Timeout); resilient.Stop();
        check("One failed pipeline request cannot stop its worker", failures == 1, "following requests continue");

        var started = Signal(); var canceled = Signal(); var fresh = Signal();
        var canceling = new ReaderBackgroundPipeline<int>(4, 1, true, false, async (request, token) =>
        {
            if (request.Payload != 1) { fresh.TrySetResult(); return; }
            started.TrySetResult();
            try { await Task.Delay(System.Threading.Timeout.Infinite, token); }
            catch (OperationCanceledException) { canceled.TrySetResult(); throw; }
        });
        canceling.Start(); canceling.TryEnqueue(1); await started.Task.WaitAsync(Timeout);
        canceling.Invalidate(); canceling.TryEnqueue(2);
        await canceled.Task.WaitAsync(Timeout); await fresh.Task.WaitAsync(Timeout); canceling.Stop();
        check("Pipeline invalidation cancels running work and accepts new chapter", canceled.Task.IsCompleted && fresh.Task.IsCompleted,
            "old download does not block the next chapter");

        var restarted = Signal(); var restartable = new ReaderBackgroundPipeline<int>(2, 1, true, false,
            (_, _) => { restarted.TrySetResult(); return Task.CompletedTask; });
        restartable.Start(); restartable.Stop(); bool stoppedRejects = !restartable.TryEnqueue(1);
        restartable.Start(); bool accepted = restartable.TryEnqueue(2);
        await restarted.Task.WaitAsync(Timeout); restartable.Stop();
        check("Pipeline can restart after Stop", stoppedRejects && accepted, "unload/reload does not leave canceled consumers");
    }

    private static async Task DiskCacheAsync(Action<string, bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "shadowviewer-image-tests-" + Guid.NewGuid().ToString("N"));
        int downloads = 0; bool failBody = false, invalidBody = false;
        var responseGate = Signal(); bool holdResponse = false;
        var bodyStarted = Signal(); bool cancelBody = false;
        UnbufferedContent? lastContent = null;
        using var client = new HttpClient(new DelegateHandler(async (_, token) =>
        {
            Interlocked.Increment(ref downloads);
            if (holdResponse) await responseGate.Task.WaitAsync(token);
            Stream body = cancelBody ? new BlockingBody(bodyStarted) : failBody ? new BrokenBody() :
                new MemoryStream(Encoding.UTF8.GetBytes(invalidBody ? "HTML error page" : "IMAGE valid payload"));
            lastContent = new UnbufferedContent(body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = lastContent };
        }));
        var cache = new ReaderImageDiskCache(client, () => directory, async (path, token) =>
            (await File.ReadAllTextAsync(path, token)).StartsWith("IMAGE", StringComparison.Ordinal) ? new Size(1000, 1400) : null);
        try
        {
            var miss = await cache.TryGetAsync("https://example.invalid/a.jpg", CancellationToken.None);
            check("Cache initialization does not download or create empty folders", miss == null && downloads == 0 && !Directory.Exists(directory),
                "network work starts only inside the preload window");
            holdResponse = true;
            var first = cache.GetAsync("https://example.invalid/a.jpg", CancellationToken.None);
            var second = cache.GetAsync("https://example.invalid/a.jpg", CancellationToken.None);
            responseGate.TrySetResult(); var images = await Task.WhenAll(first, second).WaitAsync(Timeout); holdResponse = false;
            check("Concurrent URL requests share one streamed download", downloads == 1 && images[0].Path == images[1].Path && !lastContent!.WasSerialized,
                "response body is read as a stream and one valid file is published");
            await cache.GetAsync("https://example.invalid/a.jpg", CancellationToken.None);
            check("Valid disk cache is reused", downloads == 1, "no repeat HTTP request");
            await File.WriteAllTextAsync(images[0].Path, "nonempty corrupt image");
            miss = await cache.TryGetAsync("https://example.invalid/a.jpg", CancellationToken.None);
            var repaired = await cache.GetAsync("https://example.invalid/a.jpg", CancellationToken.None);
            check("Nonempty corrupted cache is detected and replaced", miss == null && downloads == 2 && (await File.ReadAllTextAsync(repaired.Path)).StartsWith("IMAGE"),
                "file size alone does not establish cache validity");

            DateTimeOffset retryTime = DateTimeOffset.UnixEpoch;
            var decoderWindow = new ReaderImageWindowLoadController(() => retryTime);
            var decoderStrategy = new TestStrategy { Preload = async context =>
            {
                var image = await cache.GetAsync("https://example.invalid/a.jpg", context.CancellationToken);
                context.CachedFilePath = image.Path; context.Size = image.Size;
            } };
            var decoderNode = Node(0, decoderStrategy); ReaderImageLoadTicket? decoderTicket = null;
            int decodes = 0, beforeRecovery = downloads;
            var decoderService = new ReaderImageLoadService(decoderWindow, (_, _, _) =>
            {
                if (++decodes == 1) throw new IOException("valid header but corrupt pixels");
                return Task.FromResult<CanvasBitmap?>(new CanvasBitmap(0, new Size(1000, 1400)));
            }, _ => { }, _ => { }, (_, _, _) => cache.InvalidateAsync("https://example.invalid/a.jpg", CancellationToken.None));
            void UpdateDecode() => decoderWindow.UpdateWindow([decoderNode], [decoderNode], View, 0, Device,
                t => { decoderTicket = t; return true; });
            UpdateDecode(); await decoderService.LoadAsync(decoderTicket!, CancellationToken.None);
            retryTime += TimeSpan.FromSeconds(1); UpdateDecode(); await decoderService.LoadAsync(decoderTicket!, CancellationToken.None);
            check("Pixel decode failure invalidates cache before retry", decoderNode.IsLoaded && decodes == 2 && downloads == beforeRecovery + 1,
                "an image with readable dimensions can still be replaced after decoding fails");
            decoderWindow.Reset(); decoderNode.Retire();

            invalidBody = true; bool invalid = false;
            try { await cache.GetAsync("https://example.invalid/error.jpg", CancellationToken.None); }
            catch (InvalidDataException) { invalid = true; }
            check("Successful HTTP response with invalid image is not cached", invalid && Directory.GetFiles(directory).Length == 1 && Directory.GetFiles(directory, "*.tmp").Length == 0,
                "HTML or empty image bodies cannot become permanent cache entries");
            invalidBody = false;

            failBody = true; bool failed = false;
            try { await cache.GetAsync("https://example.invalid/b.jpg", CancellationToken.None); }
            catch (IOException) { failed = true; }
            check("Interrupted stream leaves no published or temporary image", failed && Directory.GetFiles(directory, "*.tmp").Length == 0 && Directory.GetFiles(directory).Length == 1,
                "a partial download cannot become a cache hit");
            failBody = false;
            await cache.GetAsync("https://example.invalid/b.jpg", CancellationToken.None);
            check("Interrupted downloads remain retryable", Directory.GetFiles(directory).Length == 2, "subsequent request downloads successfully");

            cancelBody = true;
            using var cts = new CancellationTokenSource();
            var downloading = cache.GetAsync("https://example.invalid/c.jpg", cts.Token);
            await bodyStarted.Task.WaitAsync(Timeout); cts.Cancel(); bool canceled = false;
            try { await downloading.WaitAsync(Timeout); }
            catch (OperationCanceledException) { canceled = true; }
            check("Canceled HTTP body is aborted and temporary file removed", canceled && Directory.GetFiles(directory, "*.tmp").Length == 0 && Directory.GetFiles(directory).Length == 2,
                "leaving the window releases network work without publishing partial data");
            cancelBody = false;
            await cache.GetAsync("https://example.invalid/c.jpg", CancellationToken.None);
            check("Cancellation releases the per-file lock", Directory.GetFiles(directory).Length == 3, "a later request can download the same URL");

            holdResponse = true; responseGate = Signal();
            var owner = cache.GetAsync("https://example.invalid/d.jpg", CancellationToken.None);
            int beforeWaiter = downloads;
            using var waiterCts = new CancellationTokenSource();
            var waiter = cache.GetAsync("https://example.invalid/d.jpg", waiterCts.Token); waiterCts.Cancel();
            bool waiterCanceled = false;
            try { await waiter.WaitAsync(Timeout); }
            catch (OperationCanceledException) { waiterCanceled = true; }
            responseGate.TrySetResult(); await owner.WaitAsync(Timeout); holdResponse = false;
            check("Canceling a duplicate waiter leaves active download intact", waiterCanceled && downloads == beforeWaiter && Directory.GetFiles(directory).Length == 4,
                "the canceled waiter neither releases an unowned lock nor cancels another request");
        }
        finally
        {
            string resolved = Path.GetFullPath(directory);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup path escaped the temporary directory.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
        }
    }

    private sealed class TestStrategy : IImageSourceStrategy
    {
        public int InitCalls, PreloadCalls;
        public Func<ImageLoadingContext, Task> Init = _ => Task.CompletedTask;
        public Func<ImageLoadingContext, Task> Preload = context => { context.Bytes = [1]; context.Size = new Size(1000, 1400); return Task.CompletedTask; };
        public bool CanHandle(object source) => true;
        public Task InitImageAsync(ImageLoadingContext context) { InitCalls++; return Init(context); }
        public Task PreloadImageAsync(ImageLoadingContext context) { PreloadCalls++; return Preload(context); }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class UnbufferedContent(Stream body) : HttpContent
    {
        public bool WasSerialized { get; private set; }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(body);
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => Task.FromResult(body);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        { WasSerialized = true; throw new InvalidOperationException("Response body must not be buffered."); }
    }

    private abstract class ReadOnlyBody : Stream
    {
        protected bool Sent;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected int SendPrefix(Memory<byte> buffer)
        {
            Sent = true;
            byte[] prefix = Encoding.UTF8.GetBytes("IMAGE partial"); prefix.CopyTo(buffer); return prefix.Length;
        }
    }
    private sealed class BrokenBody : ReadOnlyBody
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            !Sent ? ValueTask.FromResult(SendPrefix(buffer)) : ValueTask.FromException<int>(new IOException("interrupted body"));
    }
    private sealed class BlockingBody(TaskCompletionSource started) : ReadOnlyBody
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!Sent) return SendPrefix(buffer);
            started.TrySetResult(); await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken); return 0;
        }
    }
}
