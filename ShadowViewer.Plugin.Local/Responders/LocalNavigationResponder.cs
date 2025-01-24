using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using ShadowViewer.Interfaces;
using ShadowViewer.Models;
using ShadowViewer.Plugin.Local.Helpers;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Responders;
using ShadowViewer.Services;

using SqlSugar;

namespace ShadowViewer.Plugin.Local.Responders;

public class LocalNavigationResponder(
    ICallableService callableService,
    ISqlSugarClient sqlSugarClient,
    CompressService compressServices,
    PluginLoader pluginService,
    string id
    ) : AbstractNavigationResponder(
    callableService, sqlSugarClient, compressServices, pluginService, id
    )
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override IEnumerable<IShadowNavigationItem> NavigationViewMenuItems { get; } =
        new List<IShadowNavigationItem>
        {
            new ShadowNavigationItem(
                pluginId: LocalPlugin.Meta.Id,
                id:  "BookShelf",
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
            "BookShelf" => new ShadowNavigation(typeof(BookShelfPage), parameter: new Uri("shadow://local/")),
            _ => null
        };
    }
    public override void Navigate(Uri uri, string[] urls)
    {
        if (urls.Length == 0) return;
        switch (urls[0])
        {
            case "bookshelf":
                Caller.NavigateTo(typeof(BookShelfPage), parameter: new Uri("shadow://local/bookshelf"));
                break;
            case "settings":
                Caller.NavigateTo(typeof(BookShelfSettingsPage), parameter: new Uri("shadow://local/settings"));
                break;
            default:
                Caller.NavigateTo(typeof(BookShelfPage), parameter: new Uri("shadow://local/bookshelf"));
                break;
        }
    }
}