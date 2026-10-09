using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShadowViewer.Plugin.Local.ViewModels;

/// <summary>
/// 筛选条件的编辑副本，取消对话框不会改变已应用的条件。
/// </summary>
public partial class BookShelfFilterViewModel : ObservableObject
{
    /// <summary>标签及其选择状态。</summary>
    public ObservableCollection<BookShelfFilterOption> Tags { get; }
    /// <summary>作者及其选择状态。</summary>
    public ObservableCollection<BookShelfFilterOption> Authors { get; }

    [ObservableProperty] public partial DateTimeOffset? CreatedFrom { get; set; }
    [ObservableProperty] public partial DateTimeOffset? CreatedTo { get; set; }
    [ObservableProperty] public partial int ReadingStatusIndex { get; set; }
    [ObservableProperty] public partial double ProgressFrom { get; set; } = double.NaN;
    [ObservableProperty] public partial double ProgressTo { get; set; } = double.NaN;
    [ObservableProperty] public partial DateTimeOffset? LastReadFrom { get; set; }
    [ObservableProperty] public partial DateTimeOffset? LastReadTo { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    /// <summary>从已应用的条件及可用选项创建编辑副本。</summary>
    public BookShelfFilterViewModel(BookShelfFilter filter, IEnumerable<ShadowTag> tags,
        IEnumerable<LocalAuthor> authors)
    {
        Tags = new(tags.OrderBy(tag => tag.Name).Select(tag =>
            new BookShelfFilterOption(tag.Id, tag.Name, filter.TagIds.Contains(tag.Id))));
        Authors = new(authors.OrderBy(author => author.Name).Select(author =>
            new BookShelfFilterOption(author.Id, author.Name, filter.AuthorIds.Contains(author.Id))));
        CreatedFrom = ToOffset(filter.CreatedFrom);
        CreatedTo = ToOffset(filter.CreatedTo);
        ReadingStatusIndex = (int)filter.ReadingStatus;
        ProgressFrom = filter.ProgressFrom ?? double.NaN;
        ProgressTo = filter.ProgressTo ?? double.NaN;
        LastReadFrom = ToOffset(filter.LastReadFrom);
        LastReadTo = ToOffset(filter.LastReadTo);
    }

    /// <summary>生成独立的筛选条件，空数字输入表示不限。</summary>
    public BookShelfFilter CreateFilter() => new()
    {
        TagIds = Tags.Where(option => option.IsSelected).Select(option => option.Id).ToArray(),
        AuthorIds = Authors.Where(option => option.IsSelected).Select(option => option.Id).ToArray(),
        CreatedFrom = CreatedFrom?.Date,
        CreatedTo = CreatedTo?.Date,
        ReadingStatus = (ReadingStatus)ReadingStatusIndex,
        ProgressFrom = double.IsNaN(ProgressFrom) ? null : ProgressFrom,
        ProgressTo = double.IsNaN(ProgressTo) ? null : ProgressTo,
        LastReadFrom = LastReadFrom?.Date,
        LastReadTo = LastReadTo?.Date
    };

    [RelayCommand]
    private void Reset()
    {
        foreach (var option in Tags.Concat(Authors)) option.IsSelected = false;
        CreatedFrom = null;
        CreatedTo = null;
        ReadingStatusIndex = 0;
        ProgressFrom = double.NaN;
        ProgressTo = double.NaN;
        LastReadFrom = null;
        LastReadTo = null;
        ErrorMessage = "";
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(value.Value.Date) : null;
}
