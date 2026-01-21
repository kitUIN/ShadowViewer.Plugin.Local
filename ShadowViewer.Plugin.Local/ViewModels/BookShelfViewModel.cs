using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Configs;
using ShadowViewer.Plugin.Local.Constants;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Pages;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Sdk.Args;
using ShadowViewer.Sdk.Cache;
using ShadowViewer.Sdk.Extensions;
using ShadowViewer.Sdk.Helpers;
using ShadowViewer.Sdk.Navigation;
using ShadowViewer.Sdk.Services;
using ShadowViewer.Sdk.Utils;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace ShadowViewer.Plugin.Local.ViewModels;

/// <summary>
/// 
/// </summary>
public partial class BookShelfViewModel : ObservableObject
{
    /// <summary>
    /// 排序-<see cref="LocalSort"/>
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortDisplayName))]
    public partial LocalSort Sort { get; set; } = LocalSort.Rz;

    /// <summary>
    /// 排序显示
    /// </summary>
    public string SortDisplayName => ResourcesHelper.GetString(Sort.ToString());


    /// <summary>
    /// 返回上级
    /// </summary>
    public bool CanBackFolder => CurrentFolder.Id != -1;

    /// <summary>
    /// 当前文件夹
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBackFolder))]
    public partial LocalComic CurrentFolder { get; set; }

    /// <summary>
    /// 原始地址
    /// </summary>
    public ShadowUri OriginPath { get; private set; }


    /// <summary>
    /// 该文件夹下的漫画
    /// </summary>
    public ObservableCollection<LocalComic> LocalComics { get; } = [];

    /// <summary>
    /// 被选中的
    /// </summary>
    public ObservableCollection<LocalComic> SelectedItems { get; set; } = [];

    /// <summary>
    /// 文件夹树形结构
    /// </summary>
    public ObservableCollection<ShadowPath> FolderTree { get; } = [];

    /// <summary>
    /// 选中项的大小
    /// </summary>
    public long SelectedItemsSize => SelectedItems.Sum(x => x.Size);

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private ISqlSugarClient Db { get; }


    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private ComicIoService ComicIoService { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private ILogger Logger { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private INavigateService NavigateService { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private INotifyService NotifyService { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private IFilePickerService FilePickerService { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    public LocalPluginConfig LocalPluginConfig { get; }


    /// <summary>
    /// ConstructorInit
    /// </summary>
    partial void ConstructorInit()
    {
        SelectedItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedItemsSize));
    }

    /// <summary>
    /// 导航
    /// </summary>
    public void NavigateTo(ShadowUri uri)
    {
        var toId = -1L;
        if (uri.Query.ContainsKey("bookId"))
        {
            var path = uri.Query["bookId"][0];
            try
            {
                toId = long.Parse(path ?? "-1"); // 无参数默认顶级
            }
            catch (FormatException)
            {
            }
        }

        Logger.Information("导航到{Path},Path={P}", uri, toId);
        var current = Db.Queryable<LocalComic>().First(x => x.Id == toId);

        OriginPath = uri;
        CurrentFolder = current ?? throw new Exception("跳转失败");
        RefreshLocalComic();
        LoadFolderTree();
    }


    /// <summary>
    /// 新建文件夹
    /// </summary>
    [RelayCommand]
    private async Task CreateNewFolder()
    {
        var dialog = XamlHelper.CreateOneTextBoxDialog(I18N.NewFolder,
            I18N.NewFolderName, "", "",
            (_, _, text) =>
            {
                LocalComic.CreateFolder(text, CurrentFolder.Id);
                RefreshLocalComic();
            });

        await NotifyService.ShowDialog(this, dialog);
    }

    /// <summary>
    /// 弹出框-重命名
    /// </summary>
    [RelayCommand]
    private async Task Rename()
    {
        if (SelectedItems.Count != 1) return;
        var comic = SelectedItems[0];
        var dialog = XamlHelper.CreateOneTextBoxDialog("",
            I18N.Rename, text: comic.Name,
            primaryAction: (_, _, name) =>
            {
                Db.Updateable<LocalComic>()
                    .SetColumns(x => x.Name == name)
                    .Where(x => x.Id == comic.Id)
                    .ExecuteCommand();
                RefreshLocalComic();
            });
        await NotifyService.ShowDialog(this, dialog);
    }

    /// <summary>
    /// 菜单-查看属性
    /// </summary>
    [RelayCommand]
    private void Status()
    {
        if (SelectedItems.Count != 1) return;
        var comic = SelectedItems[0];
        NavigateService.Navigate(typeof(AttributesPage), comic.Id);
    }

    /// <summary>   
    /// 删除二次确定框
    /// </summary>
    public async Task DeleteMessageDialog()
    {
        var deleteFiles = new CheckBox()
        {
            Content = I18N.DeleteComicFiles,
            IsChecked = LocalPluginConfig.LocalIsDeleteFilesWithComicDelete,
        };
        deleteFiles.Checked += DeleteFilesChecked;
        deleteFiles.Unchecked += DeleteFilesChecked;
        var remember = new CheckBox()
        {
            Content = I18N.Remember,
            IsChecked = LocalPluginConfig.LocalIsRememberDeleteFilesWithComicDelete,
        };
        remember.Checked += RememberChecked;
        remember.Unchecked += RememberChecked;
        var dialog = new ContentDialog
        {
            Title = I18N.IsDelete,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            IsPrimaryButtonEnabled = true,
            PrimaryButtonText = I18N.Confirm,
            DefaultButton = ContentDialogButton.Close,
            CloseButtonText = I18N.Cancel,
            Content = new StackPanel()
            {
                Children = { deleteFiles, remember }
            }
        };
        dialog.PrimaryButtonClick += (_, _) => DeleteComics();
        dialog.Focus(FocusState.Programmatic);
        await NotifyService.ShowDialog(this, dialog);
        return;

        void RememberChecked(object sender, RoutedEventArgs e)
        {
            LocalPluginConfig.LocalIsRememberDeleteFilesWithComicDelete = (sender as CheckBox)?.IsChecked ?? false;
        }

        void DeleteFilesChecked(object sender, RoutedEventArgs e)
        {
            LocalPluginConfig.LocalIsDeleteFilesWithComicDelete = (sender as CheckBox)?.IsChecked ??
                                                                  false;
        }
    }

    /// <summary>
    /// 删除
    /// </summary>
    [RelayCommand]
    private async Task Delete(Page page)
    {
        if (SelectedItems.All(x => x.IsFolder))
        {
            DeleteComics();
        }
        else
        {
            if (LocalPluginConfig.LocalIsRememberDeleteFilesWithComicDelete)
                DeleteComics();
            else
                await DeleteMessageDialog();
        }
    }

    /// <summary>
    /// 导出
    /// </summary>=
    [RelayCommand]
    private async Task Export(Page page)
    {
        var exportTypes = ComicIoService.GetExportSupportType();
        var item = SelectedItems.FirstOrDefault();
        if (item == null) return;
        var file = await FilePickerService.PickSaveFileAsync(
            item.Name, 
            exportTypes, 
            PickerLocationId.Downloads, 
            "SaveFile");
        if (file == null) return;
        var token = CancellationToken.None;
        await ComicIoService.Export(file, item, page.DispatcherQueue, token);
    }

    /// <summary>
    /// 导航到书架设置
    /// </summary>
    [RelayCommand]
    private void NavigateSetting()
    {
        NavigateService.Navigate(typeof(BookShelfSettingsPage));
    }

    /// <summary>
    /// 删除选中的漫画
    /// </summary>
    private void DeleteComics()
    {
        var db = DiFactory.Services.Resolve<ISqlSugarClient>();
        foreach (var comic in SelectedItems.ToArray())
        {
            if (LocalPluginConfig.LocalIsDeleteFilesWithComicDelete && !comic.IsFolder)
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

            LocalComics.Remove(comic);
        }
    }


    /// <summary>
    /// 刷新
    /// </summary>
    [RelayCommand]
    public void RefreshLocalComic()
    {
        var comics = Db.Queryable<LocalComic>()
            .Includes(x => x.ReadingRecord)
            .Where(x => x.ParentId == CurrentFolder.Id)
            .ToList();
        if (comics.Count > 0)
        {
            switch (Sort)
            {
                case LocalSort.Az:
                    comics.Sort(LocalComic.AzSort); break;
                case LocalSort.Za:
                    comics.Sort(LocalComic.ZaSort); break;
                case LocalSort.Ca:
                    comics.Sort(LocalComic.CaSort); break;
                case LocalSort.Cz:
                    comics.Sort(LocalComic.CzSort); break;
                case LocalSort.Ra:
                    comics.Sort(LocalComic.RaSort); break;
                case LocalSort.Rz:
                    comics.Sort(LocalComic.RzSort); break;
                case LocalSort.Pa:
                    comics.Sort(LocalComic.PaSort); break;
                case LocalSort.Pz:
                    comics.Sort(LocalComic.PzSort); break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        LocalComics.Clear();
        foreach (var item in comics)
        {
            LocalComics.Add(item);
        }
        LoadFolderTree();
    }

    /// <summary>
    /// 悬浮菜单-从文件夹导入漫画
    /// </summary>
    [RelayCommand]
    private async Task AddComicFromFolder(Page page)
    {
        var folder = await FilePickerService.PickFolderAsync(
            PickerLocationId.Downloads, 
            PickerViewMode.List, 
            "AddNewComic");
        if (folder == null) return;
        var token = CancellationToken.None;
        await ComicIoService
            .Import(folder, CurrentFolder.Id, page.DispatcherQueue, token);
        RefreshLocalComic();
    }

    /// <summary>
    /// 双击
    /// </summary>
    [RelayCommand]
    private void DoubleTappedItem(LocalComic item)
    {
        if (item.IsFolder) NavigateTo(ShadowUri.Parse($"shadow://local/bookshelf?bookId={item.Id}"));
        else
        {
            if (SelectedItems.Count != 1) return;
            var comic = SelectedItems[0];
            Db.Storageable(new LocalHistory()
            {
                Id = comic.Id,
                LastReadDateTime = DateTime.Now,
                Thumb = comic.Thumb,
                Title = comic.Name,
            }).ExecuteCommand();
            NavigateService.Navigate(typeof(PicPage), new PicViewArg(PluginConstants.PluginId, comic),
                new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
        }
    }

    /// <summary>
    /// 返回上级
    /// </summary>
    [RelayCommand]
    private void BackFolder()
    {
        if (CurrentFolder.Id != -1)
            NavigateTo(ShadowUri.Parse($"shadow://local/bookshelf?bookId={CurrentFolder.ParentId}"));
    }

    /// <summary>
    /// 悬浮菜单-从压缩包导入漫画
    /// </summary>
    [RelayCommand]
    private async Task AddComicFromZip(Page page)
    {
        var files = await FilePickerService.PickMultipleFilesAsync(
            ComicIoService.GetImportSupportType(), 
            PickerLocationId.Downloads, 
            PickerViewMode.List,
            "AddComicsFromZip");
        if (!files.Any()) return;
        var token = CancellationToken.None;
        foreach (var file in files)
        {
            await ComicIoService.Import(file, CurrentFolder.Id, page.DispatcherQueue, token);
        }

        RefreshLocalComic();
    }

    /// <summary>
    /// 文件移动
    /// </summary>
    /// <param name="newFolderId"></param>
    /// <param name="comics"></param>
    public async Task MoveTo(long newFolderId, IEnumerable<LocalComic> comics)
    {
        var ids = comics.Select(x => x.Id).ToList();
        if (ids.Contains(newFolderId)) return;
        await Db.Updateable<LocalComic>()
            .SetColumns(x => x.ParentId == newFolderId)
            .SetColumns(x => x.UpdatedDateTime == DateTime.Now)
            .Where(x => ids.Contains(x.Id))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 加载文件夹树
    /// </summary>
    public void LoadFolderTree()
    {
        FolderTree.Clear();
        var selectedIds = SelectedItems.Select(x => x.Id).ToHashSet();
        var allFolders = Db.Queryable<LocalComic>()
            .Where(x => x.IsFolder && !x.IsDelete)
            .ToList();

        // 构建根节点（虚拟根目录）
        var rootComic = new LocalComic
        {
            Id = -1,
            Name = "Root",
            Thumb = "ms-appx:///Assets/Default/folder.png",
            IsFolder = true
        };
        var root = new ShadowPath(rootComic);
        BuildFolderTree(root, allFolders, selectedIds);
        FolderTree.Add(root);
    }

    /// <summary>
    /// 递归构建文件夹树
    /// </summary>
    private static void BuildFolderTree(ShadowPath parent, List<LocalComic> allFolders, HashSet<long> excludeIds)
    {
        var children = allFolders
            .Where(x => x.ParentId == parent.Id && !excludeIds.Contains(x.Id))
            .ToList();

        foreach (var childPath in children.Select(child => new ShadowPath(child)))
        {
            BuildFolderTree(childPath, allFolders, excludeIds);
            parent.Children.Add(childPath);
        }
    }

    #region 拖拽响应

    /// <summary>
    /// 
    /// </summary>
    /// <param name="comic"></param>
    [RelayCommand]
    private async Task ItemDrop(LocalComic comic)
    {
        if (!comic.IsFolder) return;
        await MoveTo(comic.Id, SelectedItems);
        RefreshLocalComic();
    }

    /// <summary>
    /// 拖动悬浮显示
    /// </summary>
    [RelayCommand]
    private void ItemDragOverCustomized(CommandWithArgs arg)
    {
        if (arg.CommandParameter is not LocalComic { IsFolder: true } comic) return;
        if (arg.Args is not DragEventArgs e) return;
        e.AcceptedOperation = comic.IsFolder && !SelectedItems.Contains(comic)
            ? DataPackageOperation.Move
            : DataPackageOperation.None;
        if (e.AcceptedOperation == DataPackageOperation.Move)
            e.DragUIOverride.Caption = I18N.MoveTo + comic.Name;
        e.DragUIOverride.IsGlyphVisible = true;
        e.DragUIOverride.IsCaptionVisible = true;
    }

    #endregion
}