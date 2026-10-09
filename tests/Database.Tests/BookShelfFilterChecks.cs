using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowViewer.Sdk.Models;

internal static class BookShelfFilterChecks
{
    private static readonly DateTime FirstDay = new(2026, 10, 1);

    public static void Seed(LocalDbContext db)
    {
        var action = new ShadowTag("Action", "#fff", "#000", null, "local", tagType: 1) { Id = 11 };
        var romance = new ShadowTag("Romance", "#fff", "#000", null, "local", tagType: 1) { Id = 12 };
        var firstAuthor = new LocalAuthor { Id = 21, Name = "First author" };
        var secondAuthor = new LocalAuthor { Id = 22, Name = "Second author" };
        ComicNode Comic(long id, decimal progress, List<ShadowTag> tags, List<LocalAuthor> authors) => new()
        {
            Id = id,
            ParentId = -1,
            NodeType = "Comic",
            Name = "Comic " + id,
            ComicDetail = new ComicDetail { Tags = tags, Authors = authors },
            ReadingRecord = new LocalReadingRecord { Percent = progress }
        };
        db.AddRange(
            Comic(1, 25.5m, [action, romance], [firstAuthor, secondAuthor]),
            Comic(2, 100m, [action], [secondAuthor]),
            Comic(3, 0m, [romance], [firstAuthor]),
            new ComicNode { Id = 4, ParentId = -1, NodeType = "Comic", Name = "Missing metadata and record" },
            Comic(5, 50m, [action], [firstAuthor]),
            new ComicNode { Id = 6, ParentId = -1, NodeType = "Folder", Name = "Folder" },
            Comic(7, 75m, [action], [firstAuthor]),
            Comic(8, 10m, [action], [firstAuthor]));
        db.AddRange(
            new LocalHistory { Id = 1, Title = "Comic 1", Thumb = "cover", LastReadDateTime = FirstDay.AddDays(3) },
            new LocalHistory { Id = 2, Title = "Comic 2", Thumb = "cover", LastReadDateTime = FirstDay.AddDays(6).AddHours(23).AddMinutes(59) },
            new LocalHistory { Id = 3, Title = "Comic 3", Thumb = "cover", LastReadDateTime = FirstDay.AddDays(8) });
        db.SaveChanges();
        db.Set<ComicNode>().Where(node => node.Id == 5).ExecuteUpdate(setters => setters.SetProperty(node => node.IsDelete, true));
        db.Set<ComicNode>().Where(node => node.Id == 7).ExecuteUpdate(setters => setters.SetProperty(node => node.ParentId, 6L));
        db.Set<ComicNode>().ExecuteUpdate(setters => setters.SetProperty(node => node.CreatedDateTime, FirstDay.AddDays(3)));
        db.Set<ComicNode>().Where(node => node.Id == 1).ExecuteUpdate(setters => setters.SetProperty(node => node.CreatedDateTime, FirstDay.AddDays(1).AddTicks(-1)));
        db.Set<ComicNode>().Where(node => node.Id == 2).ExecuteUpdate(setters => setters.SetProperty(node => node.CreatedDateTime, FirstDay.AddDays(1).AddHours(12)));
        db.Set<ComicNode>().Where(node => node.Id == 3).ExecuteUpdate(setters => setters.SetProperty(node => node.CreatedDateTime, FirstDay.AddDays(2)));
        db.Set<LocalReadingRecord>().ExecuteUpdate(setters => setters
            .SetProperty(record => record.CreatedDateTime, FirstDay)
            .SetProperty(record => record.UpdatedDateTime, FirstDay));
        db.Set<LocalReadingRecord>().Where(record => record.Id == 1).ExecuteUpdate(setters => setters.SetProperty(record => record.UpdatedDateTime, FirstDay.AddDays(7).AddHours(23)));
        db.Set<LocalReadingRecord>().Where(record => record.Id == 2).ExecuteUpdate(setters => setters.SetProperty(record => record.UpdatedDateTime, FirstDay.AddDays(5)));
        db.Set<LocalReadingRecord>().Where(record => record.Id == 8).ExecuteUpdate(setters => setters.SetProperty(record => record.UpdatedDateTime, FirstDay.AddDays(2)));
        db.ChangeTracker.Clear();
    }

    public static void TagsAndAuthors(LocalDbContext db)
    {
        var details = db.Set<ComicDetail>().Where(detail => db.Set<ComicNode>()
            .Any(node => node.Id == detail.ComicId && !node.IsDelete && node.NodeType == "Comic"));
        var tags = details.SelectMany(detail => detail.Tags!).Distinct().AsNoTracking().ToList();
        var authors = details.SelectMany(detail => detail.Authors!).Distinct().AsNoTracking().ToList();
        if (!tags.Select(tag => tag.Id).Order().SequenceEqual([11L, 12L]) ||
            !authors.Select(author => author.Id).Order().SequenceEqual([21L, 22L]))
            throw new InvalidOperationException("Filter options did not load distinct library metadata.");
        Expect(db, new() { TagIds = [11, 12] }, 1, 2, 3, 8);
        Expect(db, new() { AuthorIds = [21, 22] }, 1, 2, 3, 8);
        Expect(db, new() { TagIds = [11], AuthorIds = [21] }, 1, 8);
        Expect(db, new()
        {
            TagIds = [11, 12], AuthorIds = [21], CreatedTo = FirstDay,
            ReadingStatus = ReadingStatus.Reading, ProgressFrom = 25.5, ProgressTo = 25.5,
            LastReadFrom = FirstDay.AddDays(7), LastReadTo = FirstDay.AddDays(7)
        }, 1);
        Expect(db, new() { TagIds = [999] });
        Expect(db, new() { AuthorIds = [999] });
    }

    public static void CreationDates(LocalDbContext db)
    {
        Expect(db, new() { CreatedFrom = FirstDay, CreatedTo = FirstDay }, 1);
        Expect(db, new() { CreatedTo = FirstDay.AddDays(1) }, 1, 2);
        Expect(db, new() { CreatedFrom = FirstDay.AddDays(2) }, 3, 4, 8);
    }

    public static void Progress(LocalDbContext db)
    {
        Expect(db, new() { ReadingStatus = ReadingStatus.Unread }, 3, 4);
        Expect(db, new() { ReadingStatus = ReadingStatus.Reading }, 1, 8);
        Expect(db, new() { ReadingStatus = ReadingStatus.Completed }, 2);
        Expect(db, new() { ProgressFrom = 0, ProgressTo = 0 }, 3, 4);
        Expect(db, new() { ProgressFrom = 25.5, ProgressTo = 100 }, 1, 2);
        Expect(db, new() { ProgressTo = 10 }, 3, 4, 8);
        Expect(db, new() { ReadingStatus = ReadingStatus.Unread, ProgressFrom = 50 });
    }

    public static void LastRead(LocalDbContext db)
    {
        Expect(db, new() { LastReadFrom = FirstDay.AddDays(7), LastReadTo = FirstDay.AddDays(7) }, 1);
        Expect(db, new() { LastReadFrom = FirstDay.AddDays(6), LastReadTo = FirstDay.AddDays(6) }, 2);
        Expect(db, new() { LastReadTo = FirstDay.AddDays(2) }, 8);
        Expect(db, new() { LastReadFrom = FirstDay.AddDays(3), LastReadTo = FirstDay.AddDays(3) });
        Expect(db, new() { LastReadFrom = FirstDay.AddDays(8) }, 3);
        // 删除历史后，已有阅读进度仍可提供最后阅读时间。
        db.Set<LocalHistory>().Where(history => history.Id == 1).ExecuteDelete();
        Expect(db, new() { LastReadFrom = FirstDay.AddDays(7), LastReadTo = FirstDay.AddDays(7) }, 1);
        db.Set<LocalHistory>().Where(history => history.Id == 3).ExecuteDelete();
        Expect(db, new() { ReadingStatus = ReadingStatus.Unread, LastReadFrom = FirstDay });
        db.Set<LocalReadingRecord>().Where(record => record.Id == 8).ExecuteUpdate(setters => setters
            .SetProperty(record => record.Percent, 0m).SetProperty(record => record.LastPicture, 2));
        Expect(db, new() { LastReadTo = FirstDay.AddDays(2) }, 8);
    }

    public static void InvalidRanges(LocalDbContext db)
    {
        var invalid = new BookShelfFilter[]
        {
            new() { CreatedFrom = FirstDay.AddDays(1), CreatedTo = FirstDay },
            new() { LastReadFrom = FirstDay.AddDays(1), LastReadTo = FirstDay },
            new() { ProgressFrom = 60, ProgressTo = 50 },
            new() { ProgressFrom = -1 }, new() { ProgressTo = 101 },
            new() { ProgressFrom = double.NaN }, new() { ProgressTo = double.PositiveInfinity }
        };
        foreach (var filter in invalid)
        {
            if (filter.IsValid) throw new InvalidOperationException("Invalid filter was accepted.");
            try { filter.Apply([], new Dictionary<long, DateTime>()).ToList(); }
            catch (InvalidOperationException) { continue; }
            throw new InvalidOperationException("Invalid filter was applied.");
        }
        Expect(db, new() { CreatedFrom = FirstDay.AddHours(23), CreatedTo = FirstDay }, 1);
    }

    public static void EditAndReset(LocalDbContext db)
    {
        var applied = new BookShelfFilter
        {
            TagIds = [11], AuthorIds = [21], ReadingStatus = ReadingStatus.Reading,
            ProgressFrom = 10, ProgressTo = 90, CreatedFrom = FirstDay,
            CreatedTo = FirstDay.AddDays(3), LastReadFrom = FirstDay, LastReadTo = FirstDay.AddDays(9)
        };
        var editor = new BookShelfFilterViewModel(applied, db.Set<ShadowTag>().ToList(), db.Set<LocalAuthor>().ToList());
        Expect(db, editor.CreateFilter(), 1, 8);
        editor.Tags.Single(option => option.Id == 11).IsSelected = false;
        editor.ProgressFrom = 80;
        Expect(db, applied, 1, 8);
        editor.ResetCommand.Execute(null);
        var cleared = editor.CreateFilter();
        if (cleared.IsActive || !cleared.IsValid || editor.Tags.Any(option => option.IsSelected) ||
            editor.Authors.Any(option => option.IsSelected))
            throw new InvalidOperationException("Reset did not clear all conditions.");
        Expect(db, cleared, 1, 2, 3, 4, 6, 8);
    }

    private static void Expect(LocalDbContext db, BookShelfFilter filter, params long[] expectedIds)
    {
        var comics = db.Set<ComicNode>().AsNoTracking()
            .Include(node => node.ReadingRecord)
            .Include(node => node.ComicDetail)!.ThenInclude(detail => detail!.Tags)
            .Include(node => node.ComicDetail)!.ThenInclude(detail => detail!.Authors)
            .AsSplitQuery().Where(node => node.ParentId == -1).ToList();
        var historyTimes = db.Set<LocalHistory>().AsNoTracking().ToDictionary(history => history.Id, history => history.LastReadDateTime);
        var actualIds = filter.Apply(comics, historyTimes).Select(node => node.Id).Order().ToArray();
        if (!actualIds.SequenceEqual(expectedIds.Order()))
            throw new InvalidOperationException($"Expected [{string.Join(", ", expectedIds)}], got [{string.Join(", ", actualIds)}].");
    }
}
