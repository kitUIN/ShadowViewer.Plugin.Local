using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using ShadowViewer.Core.Args;
using ShadowViewer.Core.Cache;
using ShadowViewer.Core.Converters;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using CommunityToolkit.WinUI.Controls;
using ShadowPluginLoader.WinUI;
using Windows.Storage.Pickers;
using DryIoc.ImTools;
using ShadowViewer.Core;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Services;
using ShadowViewer.Core.Extensions;
using ShadowViewer.Core.Enums;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Services;
using System.Threading;
using ShadowViewer.Core.Models;
using ShadowViewer.Plugin.Local.ViewModels;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 书架页面
/// </summary>
public sealed partial class BookShelfPage : Page
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
        if (e.Parameter is Uri uri) ViewModel.NavigateTo(uri);
    }


    /// <summary>
    /// 右键菜单-重命名
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
    private async void ShadowCommandRename_Click(object sender, RoutedEventArgs e)
    {
        HomeCommandBarFlyout.Hide();
        var comic = ContentGridView.SelectedItems[0] as LocalComic;
        await CreateRenameDialog(ResourcesHelper.GetString(ResourceKey.Rename), XamlRoot, comic).ShowAsync();
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
    /// 菜单-查看属性
    /// </summary>
    private void ShadowCommandStatus_Click(object sender, RoutedEventArgs e)
    {
        //TODO: 正确查看属性
        HomeCommandBarFlyout.Hide();
        var comic = ContentGridView.SelectedItems[0] as LocalComic;
        Frame.Navigate(typeof(AttributesPage), comic?.Id);
    }


    /// <summary>
    /// 弹出框-重命名
    /// </summary>
    private ContentDialog CreateRenameDialog(string title, XamlRoot xamlRoot, LocalComic comic)
    {
        var dialog = XamlHelper.CreateOneLineTextBoxDialog(title, xamlRoot, comic.Name);
        dialog.PrimaryButtonClick += (s, e) =>
        {
            var name = ((TextBox)((StackPanel)((StackPanel)dialog.Content).Children[0]).Children[1]).Text;
            comic.Name = name;
            ViewModel.RefreshLocalComic();
        };
        return dialog;
    }

    /// <summary>
    /// 弹出框-新建文件夹
    /// </summary>
    /// <returns></returns>
    public ContentDialog CreateFolderDialog(XamlRoot xamlRoot, long parentId = -1)
    {
        var dialog = XamlHelper.CreateOneLineTextBoxDialog(I18N.NewFolder,
            xamlRoot, "");
        dialog.PrimaryButtonClick += (s, e) =>
        {
            var name = ((TextBox)((StackPanel)((StackPanel)s.Content).Children[0]).Children[1]).Text;
            LocalComic.CreateFolder(name, parentId);
            ViewModel.RefreshLocalComic();
        };
        return dialog;
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
    /// 拖动响应
    /// </summary>
    private void GridViewItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LocalComic { IsFolder: true } comic })
        {
            ViewModel.MoveTo(comic.Id, ViewModel.SelectedItems);
        }
    }

    /// <summary>
    /// 拖动悬浮显示
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
    private void GridViewItem_DragOverCustomized(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement frame) return;
        if (frame.Tag is LocalComic { IsFolder: true } comic)
        {
            e.DragUIOverride.Caption = I18N.MoveTo + comic.Name;
            e.AcceptedOperation = comic.IsFolder ? DataPackageOperation.Move : DataPackageOperation.None;
        }
        else
        {
            return;
        }

        e.DragUIOverride.IsGlyphVisible = true;
        e.DragUIOverride.IsCaptionVisible = true;
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

    // /// <summary>
    // /// 触控/鼠标-点击漫画项
    // /// </summary>
    // private void ContentGridView_ItemClick(object sender, ItemClickEventArgs e)
    // {
    //     if (e.ClickedItem is LocalComic comic)
    //     {
    //         if (comic.IsFolder)
    //             ViewModel.Init(new Uri(ViewModel.OriginPath, comic.Id.ToString()));
    //         else
    //         {
    //             DiFactory.Services.Resolve<ISqlSugarClient>().Storageable(new LocalHistory()
    //             {
    //                 Id = comic.Id,
    //                 LastReadDateTime = DateTime.Now,
    //                 Thumb = comic.Thumb,
    //                 Title = comic.Name,
    //             }).ExecuteCommand();
    //             Frame.Navigate(typeof(PicPage), new PicViewArg("Local", comic),
    //                 new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    //         }
    //     }
    // }
}