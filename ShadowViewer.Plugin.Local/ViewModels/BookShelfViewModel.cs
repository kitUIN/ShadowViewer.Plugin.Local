using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
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
using SharpCompress.Common;
using SharpCompress.Readers;
using Windows.Storage;
using Azure;
using NetTaste;
using Newtonsoft.Json.Linq;

namespace ShadowViewer.Plugin.Local.ViewModels;

/// <summary>
/// 
/// </summary>
public partial class BookShelfViewModel : ObservableObject
{
    /// <summary>
    /// 该文件夹内是否为空
    /// </summary>
    [ObservableProperty] private bool isEmpty = true;

    /// <summary>
    /// 文件夹内总数量
    /// </summary>
    [ObservableProperty] private int folderTotalCounts;

    /// <summary>
    /// 当前文件夹名称
    /// </summary>
    public string CurrentName { get; private set; }

    /// <summary>
    /// 当前文件夹ID
    /// </summary>
    public long ParentId { get; private set; } = -1;

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

    private ISqlSugarClient Db { get; }
    private INotifyService NotifyService { get; }
    private ComicService ComicService { get; }
    private ILogger Logger { get; }
    private readonly ICallableService caller;

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
    }

    private void Caller_RefreshBookEvent(object sender, EventArgs e)
    {
        RefreshLocalComic();
    }

    public void Init(Uri parameter)
    {
        LocalComics.CollectionChanged += LocalComics_CollectionChanged;
        OriginPath = parameter;
        var path = parameter.AbsolutePath.Split(['/',], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        ParentId = -1;
        if (path != "bookshelf")
        {
            try
            {
                ParentId = long.Parse(path ?? "-1");
            }
            catch (FormatException)
            {
            }
        }

        Logger.Information("导航到{Path},Path={P}", OriginPath, ParentId);
        RefreshLocalComic();
        CurrentName = ParentId == -1 ? "本地" : Db.Queryable<LocalComic>().First(x => x.Id == ParentId).Name;
    }

    private void LocalComics_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        IsEmpty = LocalComics.Count == 0;
        FolderTotalCounts = LocalComics.Count;
    }

    /// <summary>
    /// 刷新
    /// </summary>
    public void RefreshLocalComic()
    {
        LocalComics.Clear();
        var comics = Db.Queryable<LocalComic>()
            .Includes(x => x.ReadingRecord)
            .Where(x => x.ParentId == ParentId)
            .ToList();
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

        foreach (var item in comics)
        {
            LocalComics.Add(item);
        }
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
            await ProcessFileAsync(file, page, token);
        }
        RefreshLocalComic();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="file"></param>
    /// <param name="page"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ProcessFileAsync(IStorageItem file, Page page, CancellationToken token)
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
                Path.GetFileName(file.Path) + Core.I18n.I18N.PasswordError,
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
            LocalPlugin.Meta.Id, ParentId,
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
                            progressStackPanel.Visibility = Visibility.Collapsed;
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
}