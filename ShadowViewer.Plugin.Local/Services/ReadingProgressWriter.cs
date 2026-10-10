using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Plugin.Local.Models;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>合并连续页码变化并串行写库；SQLite 操作不能占用阅读器 UI 线程。</summary>
internal sealed class ReadingProgressWriter(Func<LocalDbContext> createContext, Action<Exception> reportError)
{
    private readonly object gate = new();
    private readonly Dictionary<long, ReadingProgressSnapshot> pending = new();
    private bool running;
    private Task worker = Task.CompletedTask;

    public void Enqueue(ReadingProgressSnapshot snapshot)
    {
        lock (gate)
        {
            pending[snapshot.ComicId] = snapshot;
            if (running) return;
            running = true;
            worker = Task.Run(WritePending);
        }
    }

    public Task FlushAsync()
    {
        lock (gate) return worker;
    }

    private void WritePending()
    {
        while (true)
        {
            ReadingProgressSnapshot snapshot;
            lock (gate)
            {
                if (pending.Count == 0)
                {
                    running = false;
                    return;
                }
                snapshot = pending.First().Value;
                pending.Remove(snapshot.ComicId);
            }
            try
            {
                using var db = createContext();
                // 切换章节后的新记录不能被旧窗口中尚未写完的页码覆盖。
                db.Set<LocalReadingRecord>()
                    .Where(x => x.Id == snapshot.ComicId && x.LastEpisode == snapshot.EpisodeOrder &&
                                x.UpdatedDateTime <= snapshot.UpdatedAt)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(x => x.LastPicture, snapshot.PageIndex)
                        .SetProperty(x => x.Percent, snapshot.Percent)
                        .SetProperty(x => x.UpdatedDateTime, snapshot.UpdatedAt));
            }
            catch (Exception ex) { reportError(ex); }
        }
    }
}

internal readonly record struct ReadingProgressSnapshot(long ComicId, int EpisodeOrder, int PageIndex,
    decimal Percent, DateTime UpdatedAt);
