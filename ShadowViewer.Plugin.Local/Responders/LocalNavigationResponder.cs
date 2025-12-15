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
                icon: new SymbolIcon(Symbol.Home),
                content: I18N.BookShelf)
        };

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override ShadowNavigation? NavigationViewItemInvokedHandler(IShadowNavigationItem item)
    {
        return item.Id switch
        {
            "BookShelf" => new ShadowNavigation(typeof(BookShelfPage), new Uri("shadow://local/bookshelf"), SelectItemId: "BookShelf"),
            _ => null
        };
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override ShadowNavigation? Navigate(Uri uri, string[] urls)
    {
        if (urls.Length == 0) return null;
        return urls[0] switch
        {
            "bookshelf" => new ShadowNavigation(typeof(BookShelfPage), new Uri("shadow://local/bookshelf"), SelectItemId: "BookShelf"),
            "settings" => new ShadowNavigation(typeof(BookShelfSettingsPage), new Uri("shadow://local/settings"), SelectItemId: "BookShelf"),
            "pictures" => new ShadowNavigation(typeof(PicPage), new Uri("shadow://local/pictures"), SelectItemId: "BookShelf"),
            _ => new ShadowNavigation(typeof(BookShelfPage), new Uri("shadow://local/bookshelf"), SelectItemId: "BookShelf")
        };
    }
}