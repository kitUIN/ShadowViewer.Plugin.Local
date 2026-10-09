using System.Linq;
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Helpers;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk;
using ShadowViewer.Sdk.Models;
using ShadowViewer.Sdk.Services;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Sdk.Database;

namespace ShadowViewer.Plugin.Local.ViewModels;

public partial class AttributesViewModel : ObservableObject
{
    /// <summary>
    /// Gets the notify service.
    /// </summary>
    [Autowired]
    public INotifyService NotifyService { get; }

    /// <summary>
    /// 最大文本宽度
    /// </summary>
    [ObservableProperty]
    public partial double TextBlockMaxWidth { get; set; }

    /// <summary>
    /// 当前漫画
    /// </summary>
    public LocalComic CurrentComic { get; set; }

    /// <summary>
    /// 新增tag
    /// </summary>
    public NewUiTag NewUiTag { get; } = new();

    /// <summary>
    /// 新增tagUI显示
    /// </summary>
    [ObservableProperty]
    public partial bool NewUiTagVisible { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public ObservableCollection<ShadowTag> Tags { get; } = [];

    /// <summary>
    /// 话
    /// </summary>
    public ObservableCollection<ComicChapter> Episodes { get; } = [];

    /// <summary>
    /// 是否有话
    /// </summary>
    public bool IsHaveEpisodes => Episodes.Count != 0;

    [Autowired] private PluginLoader PluginService { get; }
    [Autowired] private IDbContextFactory<LocalDbContext> DbFactory { get; }
    [Autowired] private ILogger Logger { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="comicId"></param>
    public void Init(long comicId)
    {
        using var db = DbFactory.CreateDbContext();
        var node = db.Set<ComicNode>()
            .Include(x => x.ReadingRecord)
            .Include(x => x.SourcePluginData)
            .FirstOrDefault(x => x.Id == comicId);
        CurrentComic = new LocalComic(node ?? throw new InvalidOperationException("Comic not found."), db);
        ReLoadTags();
        ReLoadEps();
    }

    /// <summary>
    /// 重新加载-话
    /// </summary>
    public void ReLoadEps()
    {
        using var db = DbFactory.CreateDbContext();
        Episodes.Clear();
        foreach (var item in db.Set<ComicChapter>().Where(x => x.ComicId == CurrentComic.Id).ToList())
            Episodes.Add(item);
    }

    /// <summary>
    /// 重新加载-标签
    /// </summary>
    public void ReLoadTags()
    {
        using var db = DbFactory.CreateDbContext();
        Tags.Clear();
        var affiliationTag = db.Set<ShadowTag>()
            .Where(x => x.TagType == 0 && x.PluginId == CurrentComic.Affiliation).FirstOrDefault();
        if (affiliationTag != null)
        {
            Tags.Add(affiliationTag);
        }

        foreach (var item in CurrentComic.Tags)
        {
            // item.Icon = "\uEEDB";
            Tags.Add(item);
        }
        //
        // Tags.Add(new ShadowTag(I18N.AddTag, "#FFFFFFFF",
        //     (ThemeHelper.IsDarkTheme() ? "#FFFFFFFF" : "#FF000000"),
        //     "\uE008", "", true));
    }

    /// <summary>
    /// 点击-标签
    /// </summary>
    [RelayCommand]
    private void TagClick()
    {
        NewUiTag.Clean();
        NewUiTagVisible = true;
    }

    /// <summary>
    /// 添加-标签
    /// </summary>
    [RelayCommand]
    private void AddNewTag()
    {
        using var db = DbFactory.CreateDbContext();
        if (db.Set<ShadowTag>().Where(x => x.Name == NewUiTag.Name).Any()) return;
        var tag = new ShadowTag(NewUiTag.Name, NewUiTag.BackgroundColor.ToHex(),
            NewUiTag.ForegroundColor.ToHex(),
            null, CurrentComic.Affiliation, tagType: 1);
        var detail = db.Set<ComicDetail>().Include(x => x.Tags).Single(x => x.ComicId == CurrentComic.Id);
        (detail.Tags ??= []).Add(tag);
        db.SaveChanges();
        CurrentComic.Tags.Add(tag);
        Tags.Insert(Math.Max(Tags.Count - 1, 0), tag);
        NewUiTagVisible = false;

    }

    /// <summary>
    /// 删除-标签
    /// </summary>
    public void RemoveTag(string id)
    {

    }

    /// <summary>
    /// 是否是最后一个标签
    /// </summary>
    public bool IsLastTag(ShadowTag tag)
    {
        return Tags.IndexOf(tag) == Tags.Count - 1;
    }
}
