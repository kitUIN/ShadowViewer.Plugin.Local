using DryIoc;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Core.Plugins;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Plugin.Local.Cache;

namespace ShadowViewer.Plugin.Local;

/// <summary>
/// 本地阅读器
/// </summary>
[MainPlugin(BuiltIn = true)]
[CheckAutowired]
public partial class LocalPlugin : AShadowViewerPlugin
{
    partial void ConstructorInit()
    {
        DiFactory.Services.Register<ComicService>(Reuse.Transient);
        DiFactory.Services.Register<AttributesViewModel>(Reuse.Transient);
        DiFactory.Services.Register<BookShelfSettingsViewModel>(Reuse.Transient);
        DiFactory.Services.Register<PicViewModel>(Reuse.Transient);
        DiFactory.Services.Register<BookShelfViewModel>(Reuse.Transient);
        Db.CodeFirst.InitTables<LocalEpisode>();
        Db.CodeFirst.InitTables<LocalPicture>();
        Db.CodeFirst.InitTables<CacheImg>();
        Db.CodeFirst
            .InitTables<LocalAuthor, LocalComic, LocalReadingRecord, LocalComicAuthorMapping, LocalComicTagMapping>();
        Db.CodeFirst.InitTables<LocalHistory>();
        if (!Db.Queryable<LocalComic>().Any(x => x.Id == -1L))
        {
            LocalComic.CreateFolder("root", -2, -1);
        }
    }


    /// <inheritdoc />
    public override string DisplayName => I18N.DisplayName;
}