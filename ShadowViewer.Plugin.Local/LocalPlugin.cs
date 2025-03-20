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
[CheckAutowired]
public partial class LocalPlugin : AShadowViewerPlugin
{
    partial void ConstructorInit()
    {
        DiFactory.Services.Register<ComicService>(Reuse.Transient);
        DiFactory.Services.Register<AttributesViewModel>(Reuse.Transient);
        DiFactory.Services.Register<PicViewModel>(Reuse.Transient);
        DiFactory.Services.Register<BookShelfViewModel>(Reuse.Transient);
        Db.CodeFirst.InitTables<LocalEpisode>();
        Db.CodeFirst.InitTables<LocalPicture>();
        Db.CodeFirst.InitTables<CacheImg>();
        Db.CodeFirst
            .InitTables<LocalAuthor, LocalComic, LocalReadingRecord, LocalComicAuthorMapping, LocalComicTagMapping>();
        if (!Db.Queryable<LocalComic>().Any(x => x.Id == -1L))
        {
            LocalComic.CreateFolder("root", -2, -1);
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override ShadowTag AffiliationTag { get; } = new("Local", "#ffd657", "#000000", "\uF66D", "Local");

    /// <inheritdoc />
    public override PluginMetaData MetaData => Meta;

    /// <inheritdoc />
    public override string DisplayName => "本地阅读器";
}