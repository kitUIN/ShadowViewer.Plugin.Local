using Microsoft.UI.Xaml.Controls;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.Constants;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Sdk.Models;
using ShadowViewer.Sdk.Models.Interfaces;
using ShadowViewer.Sdk.Plugins;
using ShadowViewer.Sdk.Responders;
using ShadowViewer.Sdk.Utils;
using System;
using System.Collections.Generic;
using ShadowViewer.Sdk.Navigation;

namespace ShadowViewer.Plugin.Local.Responders;

/// <summary>
/// 本地阅读器导航响应器
/// </summary>
[EntryPoint(Name = nameof(PluginResponder.NavigationResponder))]
public class LocalNavigationResponder : AbstractNavigationResponder
{
    /// <summary>
    /// 
    /// </summary>
    public LocalNavigationResponder(string id) : base(id)
    {
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override IEnumerable<IShadowNavigationItem> NavigationViewMenuItems { get; } =
        new List<IShadowNavigationItem>
        {
            new ShadowNavigationItem(
                pluginId: PluginConstants.PluginId,
                id: "BookShelf",
                uri: ShadowUri.Parse("shadow://local/bookshelf"),
                icon: new SymbolIcon(Symbol.Home),
                content: I18N.BookShelf)
        };

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void Register()
    {
        ShadowRouteRegistry.RegisterPage(new ShadowNavigation(typeof(BookShelfPage), SelectItemId: "BookShelf"),
            "local", "bookshelf");
        ShadowRouteRegistry.RegisterPage(new ShadowNavigation(typeof(BookShelfSettingsPage), SelectItemId: "BookShelf"),
            "local", "settings");
        ShadowRouteRegistry.RegisterPage(new ShadowNavigation(typeof(PicPage), SelectItemId: "BookShelf"), "local",
            "pictures");
    }
}