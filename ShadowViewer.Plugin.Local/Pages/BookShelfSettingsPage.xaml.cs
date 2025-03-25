using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.ViewModels; 

namespace ShadowViewer.Plugin.Local.Pages;

[EntryPoint(Name = "SettingsPage")]
public sealed partial class BookShelfSettingsPage : Page
{
    private BookShelfSettingsViewModel ViewModel { get; } = new BookShelfSettingsViewModel();

    public BookShelfSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
    }
}