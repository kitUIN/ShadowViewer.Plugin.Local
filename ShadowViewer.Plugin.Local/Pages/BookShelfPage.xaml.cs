using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ShadowViewer.Plugin.Local.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.System;
using Windows.UI.Core;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowViewer.Sdk.Helpers;
using ShadowViewer.Sdk.Navigation;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 书架页面
/// </summary>
public sealed partial class BookShelfPage
{
    /// <summary>
    /// ViewModel
    /// </summary>
    public BookShelfViewModel? ViewModel { get; private set; }

    /// <summary>
    /// 书架页面
    /// </summary>
    public BookShelfPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 进入页面
    /// </summary>
    /// <param name="e"></param>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel ??= DiFactory.Services.Resolve<BookShelfViewModel>();
        if (e.Parameter is ShadowUri uri)
        {
            ViewModel.NavigateTo(uri);
            return;
        }

        ViewModel?.RefreshLocalComic();
    }


    /// <summary>
    ///
    /// </summary>
    private void ShadowCommandMove_Click(object sender, RoutedEventArgs e)
    {
        HomeCommandBarFlyout.Hide();
        if (ViewModel == null) return;
        ViewModel.LoadFolderTree();
        ViewModel.MoveTeachingTipIsOpen = true;
    }


    /// <summary>
    /// 路径树-双击
    /// </summary>
    private async void TreeViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel == null) return;
        await ViewModel.MoveToPathCommand.ExecuteAsync(MoveTreeView.SelectedItem as IComicNode);
    }

    /// <summary>
    /// 检测按键
    /// </summary>
    private void GridViewOnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var view = sender as GridView;
        switch (e.Key)
        {
            case VirtualKey.A when
                WindowHelper.GetWindow(XamlRoot)
                    !.CoreWindow.GetKeyState(VirtualKey.Shift)
                    .HasFlag(CoreVirtualKeyStates.Down):
            {
                foreach (var comic in (ObservableCollection<LocalComic>)view!.ItemsSource)
                    view.SelectedItems.Add(comic);
                break;
            }
            case VirtualKey.Delete:
                if (ViewModel == null) return;
                ViewModel.DeleteCommand.Execute(null);
                break;
        }
    }

    /// <summary>
    /// 触控-下拉刷新
    /// </summary>
    private void RefreshContainer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        using var refreshCompletionDeferral = args.GetDeferral();
        ViewModel?.RefreshLocalComic();
    }
}