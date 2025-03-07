using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using ShadowViewer.Core.Models;
using ShadowViewer.Core.Models.Interfaces;
using ShadowViewer.Core.Services;
using ShadowViewer.Models;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Core.Responders;
using ShadowPluginLoader.MetaAttributes;
using ShadowViewer.Core.Utils;

namespace ShadowViewer.Plugin.Local.Responders;

/// <summary>
/// 本地阅读器导航响应器
/// </summary>
public partial class LocalNavigationResponder : AbstractNavigationResponder
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
                pluginId: LocalPlugin.Meta.Id,
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
            "BookShelf" => new ShadowNavigation(typeof(BookShelfPage), new Uri("shadow://local/")),
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
            "bookshelf" => new ShadowNavigation(typeof(BookShelfPage), new Uri("shadow://local/bookshelf")),
            "settings" => new ShadowNavigation(typeof(BookShelfSettingsPage), new Uri("shadow://local/settings")),
            "pictures" => new ShadowNavigation(typeof(PicPage), new Uri("shadow://local/pictures")),
            _ => new ShadowNavigation(typeof(BookShelfPage), new Uri("shadow://local/bookshelf"))
        };
    }
}