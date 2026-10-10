using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services;

internal static class ReadingProgressChecks
{
    public static async Task RunAsync(Func<LocalDbContext> createContext)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        using (var db = createContext())
        {
            db.AddRange(Enumerable.Range(1, 2).Select(id => new ComicNode
            {
                Id = id, Name = $"progress {id}",
                ReadingRecord = new LocalReadingRecord { LastEpisode = 2, LastPicture = 185 }
            }));
            db.SaveChanges();
        }
        var timeout = TimeSpan.FromSeconds(5);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unblock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new ConcurrentQueue<Exception>();
        int writes = 0;
        var writer = new ReadingProgressWriter(() =>
        {
            if (Interlocked.Increment(ref writes) == 1)
            {
                entered.TrySetResult();
                unblock.Task.GetAwaiter().GetResult();
            }
            return createContext();
        }, errors.Enqueue);
        var timestamp = DateTime.Now.AddSeconds(1);
        var first = new ReadingProgressSnapshot(1, 2, 185, 93m, timestamp);
        var submit = Task.Run(() => writer.Enqueue(first));
        try
        {
            await entered.Task.WaitAsync(timeout);
            await submit.WaitAsync(timeout); // Database access is blocked, but Enqueue must return.
            for (int page = 184; page >= 33; page--)
                writer.Enqueue(first with { PageIndex = page, Percent = 17m, UpdatedAt = timestamp.AddSeconds(1) });
            writer.Enqueue(new ReadingProgressSnapshot(2, 2, 44, 22.5m, timestamp.AddSeconds(2)));
        }
        finally
        {
            unblock.TrySetResult();
            await submit.WaitAsync(timeout);
            await writer.FlushAsync().WaitAsync(timeout);
        }
        Check(errors.IsEmpty && writes == 3, "Intermediate slider values were not coalesced into the latest pending write.");
        using (var db = createContext())
        {
            var records = db.Set<LocalReadingRecord>().OrderBy(x => x.Id).ToArray();
            Check(records[0].LastPicture == 33 && records[0].Percent == 17m, "Page 34 was not persisted.");
            Check(records[1].LastPicture == 44 && records[1].Percent == 22.5m, "Another book's pending progress was lost.");
            db.Set<LocalReadingRecord>().Where(x => x.Id == 1).ExecuteUpdate(setters => setters
                .SetProperty(x => x.LastEpisode, 3).SetProperty(x => x.LastPicture, 7)
                .SetProperty(x => x.UpdatedDateTime, timestamp.AddMinutes(1)));
        }
        writer.Enqueue(first with { UpdatedAt = timestamp.AddMinutes(2) });
        await writer.FlushAsync().WaitAsync(timeout);
        writer.Enqueue(first with { EpisodeOrder = 3 });
        await writer.FlushAsync().WaitAsync(timeout);
        using (var db = createContext())
        {
            var saved = db.Set<LocalReadingRecord>().Single(x => x.Id == 1);
            Check(saved.LastEpisode == 3 && saved.LastPicture == 7, "Old chapter or old timestamp overwrote newer progress.");
        }
        int attempts = 0;
        var recoveryErrors = new ConcurrentQueue<Exception>();
        var recovering = new ReadingProgressWriter(() =>
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new IOException("temporary database failure");
            return createContext();
        }, recoveryErrors.Enqueue);
        recovering.Enqueue(first);
        await recovering.FlushAsync().WaitAsync(timeout);
        recovering.Enqueue(first with { EpisodeOrder = 3, PageIndex = 12, UpdatedAt = timestamp.AddMinutes(3) });
        await recovering.FlushAsync().WaitAsync(timeout);
        using (var db = createContext())
            Check(recoveryErrors.Count == 1 && db.Set<LocalReadingRecord>().Single(x => x.Id == 1).LastPicture == 12,
                "A failed write prevented subsequent progress from saving.");
    }
}
