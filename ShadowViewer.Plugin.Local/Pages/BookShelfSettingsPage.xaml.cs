using DryIoc;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShadowViewer.Interfaces;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowViewer.ViewModels;

namespace ShadowViewer.Plugin.Local.Pages;

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