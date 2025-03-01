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
    public static ILogger Logger { get; } = Log.ForContext<BookShelfPage>();
    public BookShelfViewModel ViewModel { get; set; }
    private ICallableService caller = DiFactory.Services.Resolve<ICallableService>();

    /// <summary>
    /// 书架页面
    /// </summary>
    public BookShelfPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = DiFactory.Services.Resolve<BookShelfViewModel>();
        ViewModel.Init(e.Parameter as Uri);
    }

    /// <summary>
    /// 显示悬浮菜单
    /// </summary>
    private void ShowMenu(UIElement sender, Point? position = null)
    {
        var isComicBook = ContentGridView.SelectedItems.Count > 0;
        var isSingle = ContentGridView.SelectedItems.Count == 1;
        var myOption = new FlyoutShowOptions()
        {
            ShowMode = FlyoutShowMode.Standard,
            Position = position
        };
        ShadowCommandRename.IsEnabled = isComicBook & isSingle;
        ShadowCommandDelete.IsEnabled = isComicBook;
        ShadowCommandMove.IsEnabled = isComicBook;
        ShadowCommandStatus.IsEnabled = isComicBook & isSingle;
        HomeCommandBarFlyout.ShowAt(sender, myOption);
    }

    /// <summary>
    /// 悬浮菜单-从文件夹导入漫画
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
    private async void ShadowCommandAddFromFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileHelper.SelectFolderAsync(this, "AddNewComic");
        // if (folder != null) caller.ImportComic(new List<IStorageItem> { folder }, new string[1], 0);
        if (folder != null)
        {
            var token = CancellationToken.None;

            await DiFactory.Services.Resolve<ComicService>()
                .ImportComicFromFolderAsync(folder.Path, LocalPlugin.Meta.Id, -1, token);
        }
    }

    
    /// <summary>
    /// 右键菜单-创建文件夹
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
    private async void ShadowCommandAddNewFolder_Click(object sender, RoutedEventArgs e)
    {
        HomeCommandBarFlyout.Hide();
        await CreateFolderDialog(XamlRoot, ViewModel.ParentId).ShowAsync();
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
    /// 右键菜单-删除
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
    private void ShadowCommandDelete_Click(object sender, RoutedEventArgs e)
    {
        HomeCommandBarFlyout.Hide();
        Delete();
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
        HomeCommandBarFlyout.Hide();
        var comic = ContentGridView.SelectedItems[0] as LocalComic;
        Frame.Navigate(typeof(AttributesPage), comic?.Id);
    }

    /// <summary>
    /// 菜单-刷新
    /// </summary>
    private void ShadowCommandRefresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshLocalComic();
    }

    /// <summary>
    /// 触控/鼠标-漫画项右键<br />
    /// 选中/显示悬浮菜单
    /// </summary>
    private void ContentGridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: not null } element) return;
        var container = (GridViewItem)ContentGridView.ContainerFromItem(element.DataContext);
        if (container != null && !container.IsSelected) container.IsSelected = true;
        ShowMenu(element, e.GetPosition(element));
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
        // if (sender is FrameworkElement frame && frame.Tag is LocalComic comic && comic.IsFolder)
        // {
        //     foreach (var item in ContentGridView.SelectedItems.Cast<LocalComic>().ToList())
        //         if (!item.IsFolder)
        //             item.Parent = comic.Id;
        //     long size = 0;
        //     var db = DiFactory.Services.Resolve<ISqlSugarClient>();
        //     db.Queryable<LocalComic>().Where(x => x.Parent == comic.Id).ToList().ForEach(x => size += x.Size);
        //     comic.Size = size;
        //     comic.Update();
        //     ViewModel.RefreshLocalComic();
        // }
    }

    /// <summary>
    /// 拖动悬浮显示
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
    private void GridViewItem_DragOverCustomized(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement frame)
        {
            if (frame.Tag is LocalComic comic && comic.IsFolder)
            {
                e.DragUIOverride.Caption = ResourcesHelper.GetString(ResourceKey.MoveTo) + comic.Name;
                e.AcceptedOperation = comic.IsFolder ? DataPackageOperation.Move : DataPackageOperation.None;
            }
            else
            {
                return;
            }

            e.DragUIOverride.IsGlyphVisible = true;
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    /// <summary>
    /// 拖动初始化
    /// </summary>
    private void ContentGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        HomeCommandBarFlyout.Hide();
        foreach (LocalComic item in e.Items)
        {
            var container = (GridViewItem)ContentGridView.ContainerFromItem(item);
            if (container != null && !container.IsSelected) container.IsSelected = true;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SmokeGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    /// 修改排序
    /// </summary>
    private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        var text = ((MenuFlyoutItem)sender).Text;
        foreach (var item in SortFlyout.Items.Cast<MenuFlyoutItem>())
            item.Icon = item.Text == text ? new FontIcon() { Glyph = "\uE7B3" } : null;
        ViewModel.Sorts = EnumHelper.GetEnum<ShadowSorts>(((MenuFlyoutItem)sender).Tag.ToString());
        ViewModel.RefreshLocalComic();
        SortAppBarButton.Label = text;
    }

    /// <summary>
    /// 控件初始化
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Controls_Loaded(object sender, RoutedEventArgs e)
    {
        SelectionPanel.Visibility = Visibility.Collapsed;
        ShelfInfo.Visibility = ConfigHelper.GetBoolean(LocalSettingKey.LocalIsBookShelfInfoBar).ToVisibility();
        StyleSegmented.SelectedIndex = ConfigHelper.GetBoolean(LocalSettingKey.LocalBookStyleDetail) ? 1 : 0;
        ShadowCommandAddNewFolder.IsEnabled = ViewModel.ParentId == -1;
    }

    /// <summary>   
    /// 删除二次确定框
    /// </summary>
    public async void DeleteMessageDialog()
    {
        var dialog = XamlHelper.CreateContentDialog(XamlRoot);
        var stackPanel = new StackPanel();
        dialog.Title = ResourcesHelper.GetString(ResourceKey.IsDelete);
        var deleteFiles = new CheckBox()
        {
            Content = ResourcesHelper.GetString(ResourceKey.DeleteComicFiles),
            IsChecked = ConfigHelper.GetBoolean(LocalSettingKey.LocalIsDeleteFilesWithComicDelete),
        };
        deleteFiles.Checked += DeleteFilesChecked;
        deleteFiles.Unchecked += DeleteFilesChecked;
        var remember = new CheckBox()
        {
            Content = ResourcesHelper.GetString(ResourceKey.Remember),
            IsChecked = ConfigHelper.GetBoolean(LocalSettingKey.LocalIsRememberDeleteFilesWithComicDelete),
        };
        remember.Checked += RememberChecked;
        remember.Unchecked += RememberChecked;
        stackPanel.Children.Add(deleteFiles);
        stackPanel.Children.Add(remember);
        dialog.IsPrimaryButtonEnabled = true;
        dialog.PrimaryButtonText = ResourcesHelper.GetString(ResourceKey.Confirm);
        dialog.DefaultButton = ContentDialogButton.Close;
        dialog.CloseButtonText = ResourcesHelper.GetString(ResourceKey.Cancel);
        dialog.Content = stackPanel;
        dialog.PrimaryButtonClick += (ContentDialog s, ContentDialogButtonClickEventArgs e) => { DeleteComics(); };
        dialog.Focus(FocusState.Programmatic);
        await dialog.ShowAsync();
        return;

        void RememberChecked(object sender, RoutedEventArgs e)
        {
            LocalPlugin.Settings.LocalIsRememberDeleteFilesWithComicDelete = (sender as CheckBox)?.IsChecked ?? false;
        }

        void DeleteFilesChecked(object sender, RoutedEventArgs e)
        {
            LocalPlugin.Settings.LocalIsDeleteFilesWithComicDelete = (sender as CheckBox)?.IsChecked ??
                                                                     false;
        }
    }

    /// <summary>
    /// 删除
    /// </summary>
    private void Delete()
    {
        if (ContentGridView.SelectedItems.ToList().Cast<LocalComic>().All(x => x.IsFolder))
        {
            DeleteComics();
        }
        else
        {
            if (ConfigHelper.GetBoolean(LocalSettingKey.LocalIsRememberDeleteFilesWithComicDelete))
                DeleteComics();
            else
                DeleteMessageDialog();
        }
    }

    /// <summary>
    /// 删除选中的漫画
    /// </summary>
    private void DeleteComics()
    {
        var db = DiFactory.Services.Resolve<ISqlSugarClient>();
        foreach (LocalComic comic in ContentGridView.SelectedItems)
        {
            if (LocalPlugin.Settings.LocalIsDeleteFilesWithComicDelete && !comic.IsFolder)
            {
                comic.Link?.DeleteDirectory();
                db.Updateable<CacheZip>()
                    .SetColumns(x => x.ComicId == null)
                    .Where(x => x.ComicId == comic.Id)
                    .ExecuteCommand();
                db.Deleteable<LocalEpisode>().Where(x => x.ComicId == comic.Id).ExecuteCommand();
                db.Deleteable<LocalPicture>().Where(x => x.ComicId == comic.Id).ExecuteCommand();
                db.Deleteable<LocalComic>().Where(x => x.Id == comic.Id).ExecuteCommand();
            }
            else
            {
                db.Updateable<LocalComic>()
                    .SetColumns(x => x.IsDelete == true)
                    .Where(x => x.Id == comic.Id)
                    .ExecuteCommand();
            }
            ViewModel.LocalComics.Remove(comic);
        }
    }

    /// <summary>
    /// 检测按键
    /// </summary>
    private void GridViewOnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var view = sender as GridView;
        if (e.Key == VirtualKey.A &&
            WindowHelper.GetWindow(XamlRoot)
                !.CoreWindow.GetKeyState(VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            foreach (var comic in (ObservableCollection<LocalComic>)view!.ItemsSource)
                view.SelectedItems.Add(comic);
        else if (e.Key == VirtualKey.Delete) Delete();
    }

    /// <summary>
    /// 右键菜单-设置按钮
    /// </summary>
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        HomeCommandBarFlyout.Hide();
        Frame.Navigate(typeof(BookShelfSettingsPage), null,
            new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    /// <summary>
    /// 选中响应更改信息栏
    /// </summary>
    private void ContentGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentGridView.SelectedItems.Count > 0)
        {
            SelectionPanel.Visibility = Visibility.Visible;
            var size = ContentGridView.SelectedItems.Cast<LocalComic>().ToList().Sum(item => item.Size);
            SelectionValue.Text = ContentGridView.SelectedItems.Count.ToString();
            SizeValue.Text = CommunityToolkit.Common.Converters.ToFileSizeString(size);
        }
        else
        {
            SelectionPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 简约与详细视图切换<br />
    /// SelectedIndex:<br />
    /// 0 - 简略<br />
    /// 1 - 详细
    /// </summary>
    private void Segmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentGridView is null || sender is not Segmented se) return;
        LocalPlugin.Settings.LocalBookStyleDetail = se.SelectedIndex == 1;
        ContentGridView.ItemTemplate =
            Resources[(LocalPlugin.Settings.LocalBookStyleDetail ? "Detail" : "Simple") + "LocalComicItem"] as
                DataTemplate;
    }

    /// <summary>
    /// 触控-下拉刷新
    /// </summary>
    private void RefreshContainer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        using var refreshCompletionDeferral = args.GetDeferral();
        ViewModel.RefreshLocalComic();
    }

    /// <summary>
    /// 触控/鼠标-点击漫画项
    /// </summary>
    private void ContentGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LocalComic comic)
        {
            if (comic.IsFolder)
                ViewModel.Init(new Uri(ViewModel.OriginPath, comic.Id.ToString()));
            else
            {
                DiFactory.Services.Resolve<ISqlSugarClient>().Storageable(new LocalHistory()
                {
                    Id = comic.Id,
                    LastReadDateTime = DateTime.Now,
                    Thumb = comic.Thumb,
                    Title = comic.Name,
                }).ExecuteCommand();
                Frame.Navigate(typeof(PicPage), new PicViewArg("Local", comic),
                    new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }
    }
}