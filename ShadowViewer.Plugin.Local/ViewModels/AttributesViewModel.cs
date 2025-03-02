using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml.Media;
using Serilog;
using ShadowPluginLoader.MetaAttributes;
using ShadowViewer.Core;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Models;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using SqlSugar;
using LocalEpisode = ShadowViewer.Plugin.Local.Models.LocalEpisode;

namespace ShadowViewer.Plugin.Local.ViewModels;

public partial class AttributesViewModel : ObservableObject
{
    /// <summary>
    /// 最大文本宽度
    /// </summary>
    [ObservableProperty] private double textBlockMaxWidth;

    /// <summary>
    /// 当前漫画
    /// </summary>
    public LocalComic CurrentComic { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public ObservableCollection<ShadowTag> Tags = [];

    /// <summary>
    /// 话
    /// </summary>
    public ObservableCollection<LocalEpisode> Episodes = [];

    /// <summary>
    /// 是否有话
    /// </summary>
    public bool IsHaveEpisodes => Episodes.Count != 0;

    [Autowired] private PluginLoader PluginService { get; }
    [Autowired] private ISqlSugarClient Db { get; }
    [Autowired] private ILogger Logger { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="comicId"></param>
    public void Init(long comicId)
    {
        CurrentComic = Db.Queryable<LocalComic>().Includes(x => x.ReadingRecord).First(x => x.Id == comicId);
        ReLoadTags();
        ReLoadEps();
    }

    /// <summary>
    /// 重新加载-话
    /// </summary>
    public void ReLoadEps()
    {
        Episodes.Clear();
        foreach (var item in Db.Queryable<LocalEpisode>().Where(x => x.ComicId == CurrentComic.Id).ToList())
            Episodes.Add(item);
    }

    /// <summary>
    /// 重新加载-标签
    /// </summary>
    public void ReLoadTags()
    {
        // Tags.Clear();
        // if (PluginService.GetPlugin(CurrentComic.Affiliation) is { } p && p.AffiliationTag is { } shadow)
        // {
        //     shadow.IsEnable = false;
        //     shadow.Icon = "\uE23F";
        //     shadow.ToolTip = ResourcesHelper.GetString(ResourceKey.Affiliation) + ": " + shadow.Name;
        //     Tags.Add(shadow);
        // }
        //
        // if (CurrentComic.Tags != null)
        //     foreach (var item in CurrentComic.Tags)
        //     {
        //         item.Icon = "\uEEDB";
        //         item.ToolTip = ResourcesHelper.GetString(ResourceKey.Tag) + ": " + item.Name;
        //         Tags.Add(item);
        //     }
        //
        // Tags.Add(new ShadowTag
        // {
        //     Icon = "\uE008",
        //     // Background = (SolidColorBrush)Application.Current.LocalResources["SystemControlBackgroundBaseMediumLowBrush"],
        //     Foreground = new SolidColorBrush((ThemeHelper.IsDarkTheme() ? "#FFFFFFFF" : "#FF000000").ToColor()),
        //     IsEnable = true,
        //     Name = ResourcesHelper.GetString(ResourceKey.AddTag),
        //     ToolTip = ResourcesHelper.GetString(ResourceKey.AddTag)
        // });
    }

    /// <summary>
    /// 添加-标签
    /// </summary>
    public void AddNewTag(ShadowTag tag)
    {
        // if (Db.Queryable<ShadowTag>().First(x => x.Id == tag.Id) is ShadowTag localTag)
        // {
        //     tag.ComicId = localTag.ComicId;
        //     tag.Icon = "\uEEDB";
        //     tag.ToolTip = ResourcesHelper.GetString(ResourceKey.Tag) + ": " + localTag.Name;
        //     Db.Updateable(tag).ExecuteCommand();
        //     if (Tags.FirstOrDefault(x => x.Id == tag.Id) is ShadowTag lo) Tags[Tags.IndexOf(lo)] = tag;
        // }
        // else
        // {
        //     tag.Id = ShadowTag.RandomId();
        //     tag.ComicId = CurrentComic.Id;
        //     tag.Icon = "\uEEDB";
        //     tag.ToolTip = ResourcesHelper.GetString(ResourceKey.Tag) + ": " + tag.Name;
        //     Db.Insertable(tag).ExecuteCommand();
        //     Tags.Insert(Math.Max(0, Tags.Count - 1), tag);
        // }
    }

    /// <summary>
    /// 删除-标签
    /// </summary>
    public void RemoveTag(string id)
    {
        // if (Tags.FirstOrDefault(x => x.Id == id) is ShadowTag tag)
        // {
        //     Tags.Remove(tag);
        //     Db.Deleteable(tag).ExecuteCommand();
        // }
    }

    /// <summary>
    /// 是否是最后一个标签
    /// </summary>
    public bool IsLastTag(ShadowTag tag)
    {
        return Tags.IndexOf(tag) == Tags.Count - 1;
    }
}