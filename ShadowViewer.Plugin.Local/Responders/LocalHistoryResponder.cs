using System;
using System.Collections.Generic;
using DryIoc;
using ShadowPluginLoader.MetaAttributes;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Core.Responders;
using SqlSugar;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core.Models.Interfaces;
using ShadowViewer.Core;
using ShadowViewer.Core.Services;
using ShadowViewer.Core.Enums;

namespace ShadowViewer.Plugin.Local.Responders;

/// <summary>
/// 
/// </summary>
public partial class LocalHistoryResponder : AbstractHistoryResponder
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override IEnumerable<IHistory> GetHistories(HistoryMode mode = HistoryMode.Day) =>
        mode switch
        {
            HistoryMode.Day => Db.Queryable<LocalHistory>()
                .Where(history => history.LastReadDateTime >= DateTime.Now - TimeSpan.FromDays(1))
                .ToList(),
            HistoryMode.Week => Db.Queryable<LocalHistory>()
                .Where(history => history.LastReadDateTime >= DateTime.Now - TimeSpan.FromDays(7))
                .ToList(),
            HistoryMode.Month => Db.Queryable<LocalHistory>()
                .Where(history => history.LastReadDateTime >= DateTime.Now - TimeSpan.FromDays(30))
                .ToList(),
            _ => Db.Queryable<LocalHistory>().ToList()
        };

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void ClickHistoryHandler(IHistory history)
    {
        NavigateService.Navigate(typeof(AttributesPage), history.Id);
        Db.Updateable<LocalHistory>()
            .SetColumns(x => x.LastReadDateTime == DateTime.Now)
            .Where(x => x.Id == history.Id)
            .ExecuteCommand();
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void DeleteHistoryHandler(IHistory history)
    {
        Db.Deleteable(new LocalHistory { Id = history.Id }).ExecuteCommand();
    }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    protected INavigateService NavigateService { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    protected ISqlSugarClient Db { get; }
}