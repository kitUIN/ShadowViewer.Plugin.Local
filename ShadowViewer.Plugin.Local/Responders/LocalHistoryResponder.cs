using System.Linq;
using System;
using System.Collections.Generic;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Sdk.Responders;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Sdk.Database;
using ShadowViewer.Sdk.Enums;
using ShadowViewer.Sdk.Models.Interfaces;
using ShadowViewer.Sdk.Plugins;
using ShadowViewer.Sdk.Services;

namespace ShadowViewer.Plugin.Local.Responders;

/// <summary>
/// 
/// </summary>
[EntryPoint(Name = nameof(PluginResponder.HistoryResponder))]
public partial class LocalHistoryResponder : AbstractHistoryResponder
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override IEnumerable<IHistory> GetHistories(HistoryMode mode = HistoryMode.Day)
    {
        using var db = DbFactory.CreateDbContext();
        var since = mode switch
        {
            HistoryMode.Day => DateTime.Now.AddDays(-1),
            HistoryMode.Week => DateTime.Now.AddDays(-7),
            HistoryMode.Month => DateTime.Now.AddDays(-30),
            _ => DateTime.MinValue
        };
        return db.Set<LocalHistory>().AsNoTracking().Where(x => x.LastReadDateTime >= since).ToList();
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void ClickHistoryHandler(IHistory history)
    {
        using var db = DbFactory.CreateDbContext();
        NavigateService.Navigate(typeof(AttributesPage), history.Id);
        db.Set<LocalHistory>().Where(x => x.Id == history.Id)
            .ExecuteUpdate(setters => setters
                .SetProperty(x => x.LastReadDateTime, DateTime.Now));
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void DeleteHistoryHandler(IHistory history)
    {
        using var db = DbFactory.CreateDbContext();
        db.Set<LocalHistory>().Where(x => x.Id == history.Id).ExecuteDelete();
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
    protected IDbContextFactory<LocalDbContext> DbFactory { get; }
}
