using DryIoc;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core.Plugins;
using ShadowViewer.Plugin.Local.ViewModels;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 
/// </summary>
[EntryPoint(Name = nameof(PluginManage.SettingsPage))]
public sealed partial class BookShelfSettingsPage : Page
{

    public BookShelfSettingsViewModel ViewModel { get; } = DiFactory.Services.Resolve<BookShelfSettingsViewModel>();

    /// <summary>
    /// 
    /// </summary>
    public BookShelfSettingsPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
    }
}