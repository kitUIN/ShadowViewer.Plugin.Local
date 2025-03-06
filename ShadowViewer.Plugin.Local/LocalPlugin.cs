using System;
using System.Threading.Tasks;
using DryIoc;
using Serilog;
using ShadowPluginLoader.MetaAttributes;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Plugin.Local.ViewModels;
using SqlSugar;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Core.Plugins;
using ShadowViewer.Core.Services;
using ShadowViewer.Core;
using ShadowViewer.Core.Models;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Plugin.Local.Cache;

namespace ShadowViewer.Plugin.Local;

/// <summary>
/// 
/// </summary>
[AutoPluginMeta]
public partial class LocalPlugin : AShadowViewerPlugin
{
    /// <summary>
    /// 
    /// </summary>
    public LocalPlugin(ICallableService caller, PluginEventService pluginEventService, ISqlSugarClient db,
        CompressService compressService, ILogger logger, PluginLoader pluginService, INotifyService notifyService) :
        base(caller, db, pluginEventService, compressService, logger, pluginService, notifyService)
    {
        DiFactory.Services.Register<ComicService>(Reuse.Transient);
        DiFactory.Services.Register<AttributesViewModel>(Reuse.Transient);
        DiFactory.Services.Register<PicViewModel>(Reuse.Transient);
        DiFactory.Services.Register<BookShelfViewModel>(Reuse.Transient);
        db.CodeFirst.InitTables<LocalEpisode>();
        db.CodeFirst.InitTables<LocalPicture>();
        db.CodeFirst.InitTables<CacheImg>();
        db.CodeFirst
            .InitTables<LocalAuthor, LocalComic, LocalReadingRecord, LocalComicAuthorMapping, LocalComicTagMapping>();
    }

    /// <summary>
    /// <inheritdoc cref="AbstractPlugin.Init" />
    /// </summary>
    protected new void Init()
    {
        base.Init();
        if (!Db.Queryable<LocalComic>().Any(x => x.Id == -1L))
        {
            LocalComic.CreateFolder("root", -2, -1);
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override ShadowTag AffiliationTag { get; } = new("Local", "#ffd657", "#000000", "\uF66D", "Local");

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override Type? SettingsPage => typeof(BookShelfSettingsPage);

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override bool CanSwitch => false;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override bool CanDelete => false;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override bool CanOpenFolder => false;


    /// <inheritdoc />
    public override PluginMetaData MetaData => Meta;

    /// <inheritdoc />
    public override string DisplayName => "本地阅读器";
}