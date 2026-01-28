using System;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk.Responders;
using SqlSugar; 
using ShadowViewer.Sdk.Services;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowViewer.Sdk.Plugins;
using ShadowViewer.Plugin.Local.Entities;

namespace ShadowViewer.Plugin.Local.Responders;

/// <summary>
/// 本地图片阅读器触发器
/// </summary>
[EntryPoint(Name = nameof(PluginResponder.PicViewResponder))]
public partial class LocalPicViewResponder : AbstractPicViewResponder
{
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public override void CurrentEpisodeIndexChanged(object sender, PicViewContext ctx, int oldValue, int newValue)
    {
        if (sender is not PicViewModel viewModel) return;
        if (oldValue == newValue) return;
        if (viewModel.Affiliation != Id) return;
        viewModel.Images.Clear();
        var index = 0;
        if (viewModel.Episodes.Count <= 0 || viewModel.Episodes[newValue] is not LocalUiEpisode episode) return;

        foreach (var item in Db.Queryable<ComicPicture>().Where(x => x.ChapterId == episode.Source.Id)
                     .OrderBy(x => x.Name)
                     .ToList())
            viewModel.Images.Add(new LocalUiPicture(++index, item.StoragePath));
        var readingRecord = Db.Queryable<LocalReadingRecord>()
            .Where(x => x.Id == episode.Source.ComicId)
            .Where(x => x.LastEpisode == episode.Source.Order)
            .First();
        viewModel.CurrentPage = readingRecord is { LastPicture: >= 2 } ? readingRecord.LastPicture : 1;

        Db.Updateable<LocalReadingRecord>()
            .SetColumns(x => x.LastEpisode == episode.Source.Order)
            .SetColumns(x => x.LastPicture == viewModel.CurrentPage)
            .SetColumns(x => x.UpdatedDateTime == DateTime.Now)
            .Where(x => x.Id == episode.Source.ComicId)
            .ExecuteCommand();
    }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public override void CurrentPageIndexChanged(object sender, PicViewContext ctx,
        int oldValue, int newValue)
    {
        if (sender is not PicViewModel viewModel) return;
        if (oldValue == newValue) return;
        if (viewModel.Affiliation != Id) return;
        if (viewModel.CurrentPage <= 0 || viewModel.Comic is not { } localComic) return;
        decimal percent = 0;
        if (viewModel.CurrentEpisodeIndex >= 0 && viewModel.EpisodeCounts.Count > viewModel.CurrentEpisodeIndex &&
            localComic.Count != 0)
        {
            percent = Math.Round(
                (viewModel.EpisodeCounts[viewModel.CurrentEpisodeIndex] + viewModel.CurrentPage) /
                (decimal)localComic.Count * 100, 2);
        }

        Db.Updateable<LocalReadingRecord>()
            .SetColumns(x => x.LastPicture == viewModel.CurrentPage)
            .SetColumns(x => x.Percent == percent)
            .SetColumns(x => x.UpdatedDateTime == DateTime.Now)
            .Where(x => x.Id == localComic.Id)
            .ExecuteCommand();
    }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public override void PicturesLoadStarting(object sender, PicViewContext ctx)
    {
        if (sender is not PicViewModel viewModel) return;
        if (ctx.Affiliation != Id || ctx.Parameter is not LocalComic comic) return;
        var readingRecord = Db.Queryable<LocalReadingRecord>()
            .Where(x => x.Id == comic.Id)
            .First();
        var index = 0;
        var count = 0;
        Db.Queryable<ComicChapter>().Where(x => x.ComicId == comic.Id).OrderBy(x => x.Order).ForEach(x =>
        {
            viewModel.Episodes.Add(new LocalUiEpisode(x));
            viewModel.EpisodeCounts.Add(count);
            count += x.PageCount;
            if (readingRecord != null && readingRecord.LastEpisode == x.Order)
            {
                viewModel.CurrentEpisodeIndex = index;
            }

            index++;
        });
        if (viewModel is { CurrentEpisodeIndex: -1, Episodes.Count: > 0 })
            viewModel.CurrentEpisodeIndex = 0;
    }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    protected ICallableService Caller { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    protected ISqlSugarClient Db { get; }
}