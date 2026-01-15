using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ShadowViewer.Plugin.Local.Models;
using System;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.ViewModels;
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
        // MoveTreeView.ItemsSource = new List<ShadowPath>
        // {
        //     new(ContentGridView.SelectedItems.Cast<LocalComic>().ToList().Select(c => c.Id))
        // };
        MoveTeachingTip.IsOpen = true;
    }
 

     
    /// <summary>
    /// 路径树-双击
    /// </summary>
    private void TreeViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // MoveToPath(MoveTreeView.SelectedItem as ShadowPath);
    }

    /// <summary>
    /// 路径树-确定移动
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The arguments.</param>
    private void MoveTeachingTip_ActionButtonClick(TeachingTip sender, object args)
    {
        // MoveToPath(MoveTreeView.SelectedItem as ShadowPath);
    }

    /// <summary>
    /// 移动到路径树
    /// </summary>
    /// <param name="path">The path.</param>
    private void MoveToPath(ShadowPath path)
    {
        // if (path == null) return;
        // foreach (var comic in ContentGridView.SelectedItems.Cast<LocalComic>().ToList())
        //     if (comic.Id != path.Id && path.IsFolder)
        //         comic.Parent = path.Id;
        // long size = 0;
        // var db = DiFactory.Services.Resolve<ISqlSugarClient>();
        // db.Queryable<LocalComic>().Where(x => x.Parent == path.Id).ToList().ForEach(x => size += x.Size);
        // path.SetSize(size);
        // MoveTeachingTip.IsOpen = false;
        // ViewModel.RefreshLocalComic();
        
    }


    /// <summary>
    /// 检测按键
    /// </summary>
    private void GridViewOnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // var view = sender as GridView;
        // if (e.Key == VirtualKey.A &&
        //     WindowHelper.GetWindow(XamlRoot)
        //         !.CoreWindow.GetKeyState(VirtualKey.Shift)
        //         .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        //     foreach (var comic in (ObservableCollection<LocalComic>)view!.ItemsSource)
        //         view.SelectedItems.Add(comic);
        // else if (e.Key == VirtualKey.Delete) Delete();
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