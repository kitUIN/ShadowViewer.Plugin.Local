using DryIoc;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Plugin.Local.Cache;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using ShadowViewer.Sdk.Plugins;

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
        DiFactory.Services.Register<IComicImporter, FolderComicImporter>(Reuse.Singleton,
            made: Parameters.Of.Type(_ => MetaData.Id));
        DiFactory.Services.Register<IComicImporter, ZipComicImporter>(Reuse.Singleton,
            made: Parameters.Of.Type(_ => MetaData.Id));
        DiFactory.Services.Register<IComicExporter, ZipComicExporter>(Reuse.Singleton,
            made: Parameters.Of.Type(_ => MetaData.Id));
        DiFactory.Services.Register<ComicIoService>(Reuse.Transient);
        DiFactory.Services.Register<AttributesViewModel>(Reuse.Transient);
        DiFactory.Services.Register<PicViewModel>(Reuse.Transient);
        DiFactory.Services.Register<BookShelfViewModel>(Reuse.Transient);
        Db.CodeFirst.InitTables<ComicChapter>();
        Db.CodeFirst.InitTables<ComicPicture>();
        Db.CodeFirst.InitTables<CacheImg>();
        Db.CodeFirst
            .InitTables<ComicNode, SourcePluginData, LocalReadingRecord>();
        Db.CodeFirst
            .InitTables<LocalAuthor, ComicDetail, LocalComicAuthorMapping, LocalComicTagMapping>();
        Db.CodeFirst.InitTables<LocalHistory>();
        if (!Db.Queryable<ComicNode>().Any(x => x.Id == -1L))
        {
            ComicNode.CreateFolder("root", -2, -1);
        }
    }


    /// <inheritdoc />
    public override string DisplayName => I18N.DisplayName;
}