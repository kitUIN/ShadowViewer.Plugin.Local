using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using DryIoc;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Cache;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using ShadowViewer.Plugin.Local.ViewModels;
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
            made: Parameters.Of
                .Name("pluginId", _ => MetaData.Id)
                .Name("version", _ => MetaData.Version.ToString()));
        DiFactory.Services.Register<IComicImporter, FolderContainerComicImporter>(Reuse.Singleton,
            made: Parameters.Of
                .Name("pluginId", _ => MetaData.Id)
                .Name("version", _ => MetaData.Version.ToString()));
        DiFactory.Services.Register<IComicImporter, ZipComicImporter>(Reuse.Singleton,
            made: Parameters.Of
                .Name("pluginId", _ => MetaData.Id)
                .Name("version", _ => MetaData.Version.ToString()));
        DiFactory.Services.Register<IComicExporter, ZipComicExporter>(Reuse.Singleton,
            made: Parameters.Of
                .Name("pluginId", _ => MetaData.Id)
                .Name("version", _ => MetaData.Version.ToString()));
        DiFactory.Services.Register<ComicIoService>(Reuse.Transient);
        DiFactory.Services.Register<AttributesViewModel>(Reuse.Transient);
        DiFactory.Services.Register<PicViewModel>(Reuse.Transient);
        DiFactory.Services.Register<BookShelfViewModel>(Reuse.Transient);
        DiFactory.Services.Register<IImageSourceStrategy, LocalFileStrategy>(
            Reuse.Singleton, serviceKey: "local",
            ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        DiFactory.Services.Register<IImageSourceStrategy, NetworkStrategy>(
            Reuse.Singleton, serviceKey: "network",
            ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        using var db = DiFactory.Services.Resolve<IDbContextFactory<LocalDbContext>>().CreateDbContext();
        var source = new SourcePluginData(MetaData.Id, MetaData.Version.ToString(), MetaData.Name,
            "#ffd657", "#000000");
        var existingSource = db.Set<SourcePluginData>().Find(source.Id);
        if (existingSource == null) db.Add(source);
        else db.Entry(existingSource).CurrentValues.SetValues(source);
        db.SaveChanges();
        if (!db.Set<ComicNode>().Any(x => x.Id == -1L))
            ComicNode.CreateFolder("root", -2, -1);
    }

    /// <inheritdoc />
    protected override IEnumerable<string> ResourceDictionaries { get; } =
    [
        "ms-plugin://ShadowViewer.Plugin.Local/Themes/Converter.xaml"
    ];

    /// <inheritdoc />
    public override string DisplayName => I18N.DisplayName;
}
