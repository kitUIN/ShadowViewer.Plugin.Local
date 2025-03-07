using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core;
using ShadowViewer.Controls.Extensions;
using ShadowViewer.Core.Enums;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Services;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services;
using SqlSugar;
using SharpCompress.Readers;
using Windows.Storage;
using ShadowViewer.Core.Cache;
using ShadowViewer.Core.Extensions;
using Windows.ApplicationModel.DataTransfer;
using ShadowViewer.Core.Utils;

namespace ShadowViewer.Plugin.Local.ViewModels;

/// <summary>
/// 
/// </summary>
public partial class BookShelfViewModel : ObservableObject
{
    /// <summary>
    /// 左下角信息栏是否显示
    /// </summary>
    [ObservableProperty] private bool shelfInfo = LocalPlugin.Settings.LocalIsBookShelfInfoBar;

    /// <summary>
    /// 样式
    /// </summary>
    [ObservableProperty] private int styleIndex = LocalPlugin.Settings.LocalBookStyleDetail ? 1 : 0;

    /// <summary>
    /// 返回上级
    /// </summary>
    public bool CanBackFolder => CurrentFolder?.Id != -1;

    /// <summary>
    /// 当前文件夹
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanBackFolder))]
    private LocalComic currentFolder;

    /// <summary>
    /// 原始地址
    /// </summary>
    public Uri OriginPath { get; private set; }

    /// <summary>
    /// 排序-<see cref="ShadowSorts"/>
    /// </summary>
    public ShadowSorts Sorts { get; set; } = ShadowSorts.RZ;

    /// <summary>
    /// 该文件夹下的漫画
    /// </summary>
    public ObservableCollection<LocalComic> LocalComics { get; } = [];

    /// <summary>
    /// 被选中的
    /// </summary>
    public ObservableCollection<LocalComic> SelectedItems { get; set; } = [];

    /// <summary>
    /// 选中项的大小
    /// </summary>
    public long SelectedItemsSize => SelectedItems.Sum(x => x.Size);

    private ISqlSugarClient Db { get; }
    private INotifyService NotifyService { get; }
    private ComicService ComicService { get; }
    private ILogger Logger { get; }
    private readonly ICallableService caller;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="callableService"></param>
    /// <param name="sqlSugarClient"></param>
    /// <param name="notifyService"></param>
    /// <param name="comicService"></param>
    /// <param name="logger"></param>
    public BookShelfViewModel(ICallableService callableService,
        ISqlSugarClient sqlSugarClient, INotifyService notifyService, ComicService comicService,
        ILogger logger)
    {
        Db = sqlSugarClient;
        caller = callableService;
        NotifyService = notifyService;
        ComicService = comicService;
        caller.RefreshBookEvent += Caller_RefreshBookEvent;
        Logger = logger;
        SelectedItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedItemsSize));
    }

    private void Caller_RefreshBookEvent(object? sender, EventArgs e)
    {
        RefreshLocalComic();
    }

    /// <summary>
    /// 导航
    /// </summary>
    public void NavigateTo(Uri uri)
    {
        var splitUri = uri.AbsolutePath.Split(['/',], StringSplitOptions.RemoveEmptyEntries);
        var path = splitUri.LastOrDefault();
        var toId = -1L;
        try
        {
            toId = long.Parse(path ?? "-1"); // 无参数默认顶级
        }
        catch (FormatException)
        {
        }

        Logger.Information("导航到{Path},Path={P}", uri, toId);
        var current = Db.Queryable<LocalComic>().First(x => x.Id == toId);
        if (current == null)
        {
            // TODO: 跳转失败
            throw new Exception("跳转失败");
        }

        OriginPath = uri;
        CurrentFolder = current;
        RefreshLocalComic();
    }


    /// <summary>
    /// 新建文件夹
    /// </summary>
    [RelayCommand]
    private async Task CreateNewFolder(Page page)
    {
        var dialog = XamlHelper.CreateOneTextBoxDialog(page.XamlRoot, I18N.NewFolder,
            I18N.NewFolderName, "", "",
            (_, _, text) =>
            {
                LocalComic.CreateFolder(text, CurrentFolder!.Id);
                RefreshLocalComic();
            });
        await dialog.ShowAsync();
    }


    /// <summary>   
    /// 删除二次确定框
    /// </summary>
    public async Task DeleteMessageDialog(Page page)
    {
        var deleteFiles = new CheckBox()
        {
            Content = I18N.DeleteComicFiles,
            IsChecked = LocalPlugin.Settings.LocalIsDeleteFilesWithComicDelete,
        };
        deleteFiles.Checked += DeleteFilesChecked;
        deleteFiles.Unchecked += DeleteFilesChecked;
        var remember = new CheckBox()
        {
            Content = I18N.Remember,
            IsChecked = LocalPlugin.Settings.LocalIsRememberDeleteFilesWithComicDelete,
        };
        remember.Checked += RememberChecked;
        remember.Unchecked += RememberChecked;
        var dialog = new ContentDialog
        {
            Title = I18N.IsDelete,
            XamlRoot = page.XamlRoot,
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
    [RelayCommand]
    private async Task Delete(Page page)
    {
        if (SelectedItems.All(x => x.IsFolder))
        {
            DeleteComics();
        }
        else
        {
            if (LocalPlugin.Settings.LocalIsRememberDeleteFilesWithComicDelete)
                DeleteComics();
            else
                await DeleteMessageDialog(page);
        }
    }

    /// <summary>
    /// 删除选中的漫画
    /// </summary>
    private void DeleteComics()
    {
        var db = DiFactory.Services.Resolve<ISqlSugarClient>();
        foreach (var comic in SelectedItems)
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
            switch (Sorts)
            {
                case ShadowSorts.AZ:
                    comics.Sort(LocalComic.AzSort); break;
                case ShadowSorts.ZA:
                    comics.Sort(LocalComic.ZaSort); break;
                case ShadowSorts.CA:
                    comics.Sort(LocalComic.CaSort); break;
                case ShadowSorts.CZ:
                    comics.Sort(LocalComic.CzSort); break;
                case ShadowSorts.RA:
                    comics.Sort(LocalComic.RaSort); break;
                case ShadowSorts.RZ:
                    comics.Sort(LocalComic.RzSort); break;
                case ShadowSorts.PA:
                    comics.Sort(LocalComic.PaSort); break;
                case ShadowSorts.PZ:
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
    }

    /// <summary>
    /// 悬浮菜单-从文件夹导入漫画
    /// </summary>
    [RelayCommand]
    private async Task AddComicFromFolder(Page page)
    {
        var folder = await FileHelper.SelectFolderAsync(page, "AddNewComic");
        // if (folder != null) caller.ImportComic(new List<IStorageItem> { folder }, new string[1], 0);
        if (folder == null) return;
        var token = CancellationToken.None;

        await Task.Run(() => DiFactory.Services.Resolve<ComicService>()
            .ImportComicFromFolderAsync(folder.Path, LocalPlugin.Meta.Id, CurrentFolder.Id), token);
        RefreshLocalComic();
    }

    /// <summary>
    /// 双击
    /// </summary>
    [RelayCommand]
    private void DoubleTappedItem(LocalComic item)
    {
        if (item.IsFolder) NavigateTo(new Uri($"shadow://local/bookshelf/{item.Id}"));
        // TODO 跳转到漫画y
    }

    /// <summary>
    /// 返回上级
    /// </summary>
    [RelayCommand]
    private void BackFolder()
    {
        if (CurrentFolder!.Id != -1) NavigateTo(new Uri($"shadow://local/bookshelf/{CurrentFolder.ParentId}"));
    }

    /// <summary>
    /// 悬浮菜单-从压缩包导入漫画
    /// </summary>
    [RelayCommand]
    private async Task AddComicFromZip(Page page)
    {
        var files = await FileHelper.SelectMultipleFileAsync(page,
            "AddComicsFromZip", PickerViewMode.List, ".zip", ".rar", ".7z");
        if (!files.Any()) return;
        var token = CancellationToken.None;
        foreach (var file in files)
        {
            await DecompressComicAsync(file, page, token);
        }

        RefreshLocalComic();
    }


    /// <summary>
    /// 解压漫画文件
    /// </summary>
    /// <param name="file"></param>
    /// <param name="page"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task DecompressComicAsync(IStorageItem file, Page page, CancellationToken token)
    {
        var progressRing = new ProgressRing()
        {
            Width = 20,
            Height = 20,
            Maximum = 100,
            Value = 0,
        };
        var progressRingBackground = new ProgressRing()
        {
            Width = 20,
            Height = 20,
            Maximum = 100,
            Value = 100,
            IsIndeterminate = false,
            Visibility = Visibility.Collapsed,
            Opacity = 0.3,
        };
        var progressRingText = new TextBlock()
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16,
            Visibility = Visibility.Collapsed,
            Text = "0.00%"
        };
        var zipThumb = new Image()
        {
            Width = 120,
            Height = 160,
            Visibility = Visibility.Collapsed
        };
        var progressStackPanel = new StackPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10,
            Orientation = Orientation.Horizontal,
            Children =
            {
                new Grid()
                {
                    Children =
                    {
                        progressRingBackground,
                        progressRing
                    }
                },
                progressRingText
            }
        };
        var infoBar = new InfoBar()
        {
            Title = I18N.ImportComic + ": " + Path.GetFileNameWithoutExtension(file.Path),
            Severity = InfoBarSeverity.Informational,
            IsClosable = false,
            IsIconVisible = true,
            IsOpen = true,
            FlowDirection = FlowDirection.LeftToRight,
            Content = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 45, 18),
                Spacing = 10,
                Children =
                {
                    zipThumb,
                    progressStackPanel,
                }
            }
        };
        NotifyService.NotifyTip(
            this, infoBar, 0, TipPopupPosition.Right);
        var op = new ReaderOptions();
        var passed = false;
        while (!passed) // 检查压缩包密码
        {
            passed = await Task.Run(() => ComicService.CheckPassword(file.Path, op), token);
            if (passed) break;
            infoBar.Severity = InfoBarSeverity.Warning;
            infoBar.Title = I18N.NeedPassword + ": " + Path.GetFileNameWithoutExtension(file.Path);
            var dialog = XamlHelper.CreateOneTextBoxDialog(page.XamlRoot,
                Core.I18n.I18N.PasswordError,
                "", Core.I18n.I18N.ZipPasswordPlaceholder, "",
                (_, _, text) => op.Password = text);
            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.None) break;
        }

        if (!passed) // 如果取消输入密码
        {
            await infoBar.Close(0.5);
            return;
        }

        if (infoBar.Severity == InfoBarSeverity.Warning)
        {
            infoBar.Severity = InfoBarSeverity.Informational;
            infoBar.Title = I18N.ImportComic + ": " + Path.GetFileNameWithoutExtension(file.Path);
        }

        progressRing.IsIndeterminate = false;
        progressRingBackground.Visibility = Visibility.Visible;
        progressRingText.Visibility = Visibility.Visible;
        await Task.Run(() => ComicService.ImportComicFromZipAsync(file.Path,
            CoreSettings.ComicsPath,
            LocalPlugin.Meta.Id, CurrentFolder.Id,
            new Progress<MemoryStream>(async void (thumbStream) =>
            {
                try
                {
                    await page.DispatcherQueue.EnqueueAsync(async () =>
                    {
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(thumbStream.AsRandomAccessStream());
                        zipThumb.Source = bitmapImage;
                        zipThumb.Visibility = Visibility.Visible;
                    });
                }
                catch (Exception e)
                {
                    Logger.Error("上报缩略图报错: {e}", e);
                }
            }),
            new Progress<double>(async void (v) =>
            {
                try
                {
                    await page.DispatcherQueue.EnqueueAsync(async () =>
                    {
                        progressRing.Value = v;
                        progressRingText.Text = $"{v:0.00}%";
                        if (Math.Abs(v - 100) == 0)
                        {
                            infoBar.Severity = InfoBarSeverity.Success;
                            infoBar.Title = I18N.ImportComicSuccess;
                            await infoBar.Close();
                        }
                    });
                }
                catch (Exception e)
                {
                    Logger.Error("上报进度报错: {e}", e);
                }
            }), op), token);
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
        if(e.AcceptedOperation == DataPackageOperation.Move)
            e.DragUIOverride.Caption = I18N.MoveTo + comic.Name;
        e.DragUIOverride.IsGlyphVisible = true;
        e.DragUIOverride.IsCaptionVisible = true;
    }

    #endregion
}