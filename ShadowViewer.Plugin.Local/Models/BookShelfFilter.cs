using ShadowViewer.Plugin.Local.Enums;
using System;
using System.Collections.Generic;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 已应用的书架筛选条件。同类标签、作者满足任一，不同条件同时满足。
/// </summary>
public sealed class BookShelfFilter
{
    /// <summary>选中标签的数据库 ID，空集合表示不限。</summary>
    public IReadOnlyList<long> TagIds { get; init; } = [];
    /// <summary>选中作者的数据库 ID，空集合表示不限。</summary>
    public IReadOnlyList<long> AuthorIds { get; init; } = [];
    /// <summary>创建日期下限，包含当天。</summary>
    public DateTime? CreatedFrom { get; init; }
    /// <summary>创建日期上限，包含当天。</summary>
    public DateTime? CreatedTo { get; init; }
    /// <summary>阅读状态。</summary>
    public ReadingStatus ReadingStatus { get; init; }
    /// <summary>进度百分比下限，包含边界。</summary>
    public double? ProgressFrom { get; init; }
    /// <summary>进度百分比上限，包含边界。</summary>
    public double? ProgressTo { get; init; }
    /// <summary>最后阅读日期下限，包含当天。</summary>
    public DateTime? LastReadFrom { get; init; }
    /// <summary>最后阅读日期上限，包含当天。</summary>
    public DateTime? LastReadTo { get; init; }

    /// <summary>是否设置了任一筛选条件。</summary>
    public bool IsActive => TagIds.Count > 0 || AuthorIds.Count > 0 || CreatedFrom.HasValue ||
        CreatedTo.HasValue || ReadingStatus != ReadingStatus.All || ProgressFrom.HasValue ||
        ProgressTo.HasValue || LastReadFrom.HasValue || LastReadTo.HasValue;

    /// <summary>日期和百分比区间是否合法。</summary>
    public bool IsValid => ValidDates(CreatedFrom, CreatedTo) && ValidDates(LastReadFrom, LastReadTo) &&
        ValidProgress(ProgressFrom) && ValidProgress(ProgressTo) &&
        (!ProgressFrom.HasValue || !ProgressTo.HasValue || ProgressFrom <= ProgressTo);

    private static bool ValidDates(DateTime? from, DateTime? to) =>
        !from.HasValue || !to.HasValue || from.Value.Date <= to.Value.Date;

    private static bool ValidProgress(double? value) =>
        !value.HasValue || double.IsFinite(value.Value) && value.Value is >= 0 and <= 100;
}
