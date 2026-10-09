using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 书架组合筛选及最后阅读时间的判定。
/// </summary>
public static class BookShelfFilterService
{
    /// <summary>
    /// 日期区间包含结束当天；初始阅读记录的创建时间不视为阅读时间。
    /// </summary>
    public static IEnumerable<ComicNode> Apply(this BookShelfFilter filter, IEnumerable<ComicNode> comics,
        IReadOnlyDictionary<long, DateTime> lastReadTimes)
    {
        if (!filter.IsValid) throw new InvalidOperationException("Invalid bookshelf filter range.");
        var tagIds = filter.TagIds.ToHashSet();
        var authorIds = filter.AuthorIds.ToHashSet();
        return comics.Where(comic => Matches(filter, comic, lastReadTimes, tagIds, authorIds));
    }

    private static bool Matches(BookShelfFilter filter, ComicNode comic,
        IReadOnlyDictionary<long, DateTime> lastReadTimes, HashSet<long> tagIds, HashSet<long> authorIds)
    {
        if (comic.IsDelete) return false;
        if (!filter.IsActive) return true;
        if (comic.NodeType != "Comic") return false;
        if (tagIds.Count > 0 && comic.ComicDetail?.Tags?.Any(tag => tagIds.Contains(tag.Id)) != true)
            return false;
        if (authorIds.Count > 0 && comic.ComicDetail?.Authors?.Any(author => authorIds.Contains(author.Id)) != true)
            return false;
        if (!InDateRange(comic.CreatedDateTime, filter.CreatedFrom, filter.CreatedTo)) return false;

        var progress = (double)(comic.ReadingRecord?.Percent ?? 0);
        if (filter.ReadingStatus == ReadingStatus.Unread && progress > 0 ||
            filter.ReadingStatus == ReadingStatus.Reading && (progress <= 0 || progress >= 100) ||
            filter.ReadingStatus == ReadingStatus.Completed && progress < 100)
            return false;
        if (filter.ProgressFrom.HasValue && progress < filter.ProgressFrom.Value ||
            filter.ProgressTo.HasValue && progress > filter.ProgressTo.Value)
            return false;

        if (!filter.LastReadFrom.HasValue && !filter.LastReadTo.HasValue) return true;
        DateTime? lastRead = lastReadTimes.TryGetValue(comic.Id, out var historyTime) && historyTime > DateTime.MinValue
            ? historyTime : null;
        var record = comic.ReadingRecord;
        if (record != null && (record.Percent > 0 || record.LastPicture > 0 || record.LastEpisode > 0) &&
            record.UpdatedDateTime > DateTime.MinValue &&
            (!lastRead.HasValue || record.UpdatedDateTime > lastRead.Value))
            lastRead = record.UpdatedDateTime;
        return lastRead.HasValue && InDateRange(lastRead.Value, filter.LastReadFrom, filter.LastReadTo);
    }

    private static bool InDateRange(DateTime value, DateTime? from, DateTime? to) =>
        (!from.HasValue || value.Date >= from.Value.Date) && (!to.HasValue || value.Date <= to.Value.Date);
}
