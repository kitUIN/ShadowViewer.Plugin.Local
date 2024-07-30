using System;
using System.Collections.Generic;
using System.Linq;
using DryIoc;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using ShadowViewer.Interfaces;
using ShadowViewer.Models;
using ShadowViewer.Plugins;
using ShadowViewer.Services;
using ShadowPluginLoader.MetaAttributes;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowViewer.ViewModels;
using SqlSugar;
using ShadowPluginLoader.WinUI;

namespace ShadowViewer.Plugin.Local;

[AutoPluginMeta]
public partial class LocalPlugin : AShadowViewerPlugin
{
    public LocalPlugin(ICallableService callableService, 
        ISqlSugarClient sqlSugarClient,
        CompressService compressServices, 
        PluginLoader pluginService, 
        ILogger logger) : 
        base(callableService, 
            sqlSugarClient, 
            compressServices, 
            pluginService, logger)
    {
        DiFactory.Services.Register<AttributesViewModel>(Reuse.Transient);
        DiFactory.Services.Register<PicViewModel>(Reuse.Transient);
        DiFactory.Services.Register<BookShelfViewModel>(Reuse.Transient);
    }


    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override LocalTag AffiliationTag { get; } = new LocalTag("Local", "#000000", "#ffd657");

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