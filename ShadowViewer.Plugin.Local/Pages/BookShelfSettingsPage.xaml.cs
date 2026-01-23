using DryIoc;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Configs;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 
/// </summary>
[EntryPoint(Name = "SettingsPage")]
public sealed partial class BookShelfSettingsPage : Page
{
    /// <summary>
    /// ViewModel
    /// </summary>
    public LocalPluginConfig Config { get; } = DiFactory.Services.Resolve<LocalPluginConfig>();

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