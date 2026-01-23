using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ShadowViewer.Plugin.Local.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using ShadowPluginLoader.WinUI;
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
    public BookShelfViewModel ViewModel { get; private set; } = null!;

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
        ViewModel = DiFactory.Services.Resolve<BookShelfViewModel>();
        if (e.Parameter is ShadowUri uri) ViewModel.NavigateTo(uri);
    }

     
    /// <summary>
    ///
    /// </summary>
    private void ShadowCommandMove_Click(object sender, RoutedEventArgs e)
    {
        HomeCommandBarFlyout.Hide();
        ViewModel.LoadFolderTree();
        MoveTeachingTip.IsOpen = true;
    }
 

     
    /// <summary>
    /// 路径树-双击
    /// </summary>
    private async void TreeViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        await MoveToPath(MoveTreeView.SelectedItem as ShadowPath);
    }

    /// <summary>
    /// 路径树-确定移动
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The arguments.</param>
    private async void MoveTeachingTip_ActionButtonClick(TeachingTip sender, object args)
    {
        await MoveToPath(MoveTreeView.SelectedItem as ShadowPath);
    }

    /// <summary>
    /// 移动到路径树
    /// </summary>
    /// <param name="path">The path.</param>
    private async Task MoveToPath(ShadowPath path)
    {
        if (path == null) return;

        var selectedComics = ViewModel.SelectedItems.ToList();
        await ViewModel.MoveTo(path.Id, selectedComics);

        MoveTeachingTip.IsOpen = false;
        ViewModel.RefreshLocalComic();
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
        ViewModel.RefreshLocalComic();
    }
}