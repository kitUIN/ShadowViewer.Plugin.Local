﻿using System.Collections.Generic;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Core.Responders;
using SqlSugar;
using PicViewModel = ShadowViewer.Plugin.Local.ViewModels.PicViewModel;
using ShadowViewer.Core.Models;
using ShadowViewer.Core.Args;
using ShadowViewer.Core;
using ShadowViewer.Core.Services;

namespace ShadowViewer.Plugin.Local.Responders;

public class LocalPicViewResponder : AbstractPicViewResponder
{
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public override void CurrentEpisodeIndexChanged(object sender, string affiliation, int oldValue, int newValue)
    {
        if (sender is not PicViewModel viewModel) return;
        if (oldValue == newValue) return;
        if (viewModel.Affiliation != Id) return;
        viewModel.Images.Clear();
        var index = 0;
        if (viewModel.Episodes.Count <= 0 || viewModel.Episodes[newValue] is not LocalUiEpisode episode) return;
        foreach (var item in Db.Queryable<LocalPicture>().Where(x => x.EpisodeId == episode.Source.Id)
                     .OrderBy(x => x.Name)
                     .ToList())
            viewModel.Images.Add(new LocalUiPicture(++index, item.Img));
    }

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public override void PicturesLoadStarting(object sender, PicViewArg arg)
    {
        if (sender is not PicViewModel viewModel) return;
        if (arg.Affiliation != Id || arg.Parameter is not LocalComic comic) return;
        var orders = new List<int>();
        Db.Queryable<LocalEpisode>().Where(x => x.ComicId == comic.Id).OrderBy(x => x.Order).ForEach(x =>
        {
            orders.Add(x.Order);
            viewModel.Episodes.Add(new LocalUiEpisode(x));
        });
        if (viewModel.CurrentEpisodeIndex == -1 && orders.Count > 0)
            viewModel.CurrentEpisodeIndex = orders[0];
    }

    public LocalPicViewResponder(ICallableService callableService, ISqlSugarClient sqlSugarClient,
        CompressService compressServices, PluginLoader pluginService, string id) : base(callableService,
        sqlSugarClient, compressServices, pluginService, id)
    {
    }
}