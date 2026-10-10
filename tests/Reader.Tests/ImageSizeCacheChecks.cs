using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using ShadowViewer.Plugin.Local.Readers.Internal;
using Windows.Foundation;

internal static class ImageSizeCacheChecks
{
    public static async Task RunAsync(Action<string, bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "reader-size-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string cachePath = Path.Combine(directory, "index.json");
            string imagePath = Path.Combine(directory, "page.bin");
            await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
            int probes = 0;
            Task<Size> Probe(CancellationToken _) { probes++; return Task.FromResult(new Size(2000, 1400)); }
            var cache = new ReaderImageSizeCache(cachePath);
            await cache.GetAsync(imagePath, Probe, CancellationToken.None);
            await cache.GetAsync(imagePath, Probe, CancellationToken.None);
            check("Unchanged image reuses dimensions in memory", probes == 1,
                "reopening a page does not call the expensive size probe again");
            await cache.FlushAsync(CancellationToken.None);
            cache = new ReaderImageSizeCache(cachePath);
            var restored = await cache.GetAsync(imagePath, Probe, CancellationToken.None);
            check("Image dimensions survive a new cache instance", probes == 1 && restored.Width == 2000 && restored.Height == 1400,
                "a process restart can restore dimensions from the persisted index");

            DateTime originalTime = File.GetLastWriteTimeUtc(imagePath);
            await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
            File.SetLastWriteTimeUtc(imagePath, originalTime);
            await cache.GetAsync(imagePath, Probe, CancellationToken.None);
            check("Length change invalidates cached dimensions", probes == 2,
                "replacement is detected even when the modification timestamp is preserved");
            File.SetLastWriteTimeUtc(imagePath, originalTime.AddMinutes(1));
            await cache.GetAsync(imagePath, Probe, CancellationToken.None);
            check("Timestamp change invalidates cached dimensions", probes == 3,
                "an edit with unchanged file length still triggers a fresh size probe");

            string changedDuringProbe = Path.Combine(directory, "changing.bin");
            await File.WriteAllBytesAsync(changedDuringProbe, [1]);
            int changingProbes = 0;
            await cache.GetAsync(changedDuringProbe, async _ =>
            {
                changingProbes++;
                await File.WriteAllBytesAsync(changedDuringProbe, [1, 2]);
                return new Size(1000, 1400);
            }, CancellationToken.None);
            await cache.GetAsync(changedDuringProbe, _ =>
            {
                changingProbes++;
                return Task.FromResult(new Size(2000, 1400));
            }, CancellationToken.None);
            check("File changes during a probe are not cached", changingProbes == 2,
                "dimensions are associated only with the file version actually probed");

            string invalid = Path.Combine(directory, "invalid.bin");
            await File.WriteAllBytesAsync(invalid, [0]);
            int invalidProbes = 0;
            Task<Size> Invalid(CancellationToken _) { invalidProbes++; return Task.FromResult(new Size(0, 0)); }
            await cache.GetAsync(invalid, Invalid, CancellationToken.None);
            await cache.GetAsync(invalid, Invalid, CancellationToken.None);
            check("Unknown dimensions never become cache hits", invalidProbes == 2,
                "failed or placeholder dimensions remain retryable");

            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                bool wasCanceled = false;
                try { await cache.GetAsync(imagePath, Probe, canceled.Token); }
                catch (OperationCanceledException) { wasCanceled = true; }
                check("Canceled cache lookup skips probing", wasCanceled && probes == 3,
                    "a retired chapter cannot start new image work");
            }

            await cache.FlushAsync(CancellationToken.None);
            check("Cache publication leaves no temporary files", Directory.GetFiles(directory, "*.tmp").Length == 0,
                "the persisted snapshot is replaced atomically");
            await File.WriteAllTextAsync(cachePath, "{broken");
            cache = new ReaderImageSizeCache(cachePath);
            await cache.GetAsync(imagePath, Probe, CancellationToken.None);
            await cache.FlushAsync(CancellationToken.None);
            cache = new ReaderImageSizeCache(cachePath);
            await cache.GetAsync(imagePath, Probe, CancellationToken.None);
            check("Corrupt index is rebuilt and persisted", probes == 4,
                "a damaged cache causes one fresh probe, then becomes reusable again");

            string unwritable = Path.Combine(directory, "not-a-directory");
            await File.WriteAllTextAsync(unwritable, "file");
            var unavailableCache = new ReaderImageSizeCache(Path.Combine(unwritable, "index.json"));
            var readable = await unavailableCache.GetAsync(imagePath, Probe, CancellationToken.None);
            await unavailableCache.FlushAsync(CancellationToken.None);
            check("Unavailable cache storage does not block reading", readable.Width == 2000,
                "size probing and in-memory reuse work even if the index cannot be written");

            await CheckChapterReopen(directory, check);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task CheckChapterReopen(string directory, Action<string, bool, string> check)
    {
        string cachePath = Path.Combine(directory, "chapter.json");
        var paths = Enumerable.Range(0, 80).Select(i => Path.Combine(directory, $"chapter-{i}.bin")).ToArray();
        foreach (string path in paths) await File.WriteAllBytesAsync(path, [1]);
        int probes = 0;
        async Task<List<RenderNode>> Open(ReaderImageSizeCache cache)
        {
            var nodes = await Task.WhenAll(paths.Select(async (path, i) =>
            {
                var size = await cache.GetAsync(path, _ =>
                {
                    Interlocked.Increment(ref probes);
                    return Task.FromResult(new Size(i % 8 == 0 ? 2000 : 1000, 1400));
                }, CancellationToken.None);
                var node = new RenderNode { Source = path, Ctx = new ImageLoadingContext { Source = path } };
                node.TryApplyImageContext(new ImageLoadingContext { Source = path, Size = size, CachedFilePath = path });
                return node;
            }));
            await cache.FlushAsync(CancellationToken.None);
            return nodes.ToList();
        }

        var first = await Open(new ReaderImageSizeCache(cachePath));
        var second = await Open(new ReaderImageSizeCache(cachePath));
        check("Reopening a cached chapter requires zero additional size probes", probes == paths.Length &&
            second.All(n => n.IsSizeLoaded && n.Ctx.HasValidSize),
            "all 80 image dimensions are restored from disk using only file metadata validation");
        var preparation = new ReaderPageIndexPreparation();
        preparation.Add(second);
        check("Restored dimensions need no pending preparation work", preparation.IsReady,
            "the complete dimension set can be paginated immediately");
        foreach (var mode in new[] { ReadingMode.SpreadLtr, ReadingMode.SpreadRtl })
        {
            var pages = new List<RenderNode>();
            ReaderPageSequence.Update(first, pages, mode, 0);
            var expected = pages.Select(n => (n.Source, n.SourceRegion.X, n.SourceRegion.Width)).ToArray();
            var reopened = new List<RenderNode>();
            ReaderPageSequence.Update(second, reopened, mode, 0);
            check($"{mode} persisted dimensions reconstruct the same page index", reopened.Count == 90 &&
                reopened.Select(n => (n.Source, n.SourceRegion.X, n.SourceRegion.Width)).SequenceEqual(expected),
                "split order and total page count are stable across cache instances");
            ReaderPageSequence.Update(second, reopened, mode, 0, splitWidePages: false);
            check($"{mode} dimension cache respects the current split setting", reopened.Count == 80,
                "the cache stores original dimensions, not a stale mode-specific page list");
        }
        await File.WriteAllBytesAsync(paths[10], [1, 2]);
        await Open(new ReaderImageSizeCache(cachePath));
        check("Chapter reopen re-probes only changed images", probes == paths.Length + 1,
            "79 unchanged images retain their cached dimensions");
    }
}
