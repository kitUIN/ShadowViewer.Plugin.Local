using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Controls.Extensions;
using ShadowViewer.Sdk.Enums;
using ShadowViewer.Sdk.Services;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using ShadowViewer.Sdk.Extensions;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// Zip导出器
/// </summary>
public partial class ZipComicExporter : IComicExporter
{
    /// <inheritdoc />
    public bool Check(IStorageItem item)
    {
        return item is StorageFile file && SupportTypes.SelectMany(x => x.Value).ToArray().ContainsIgnoreCase(file.FileType);
    }

    /// <summary>
    /// NotifyService
    /// </summary>
    [Autowired]
    protected INotifyService NotifyService { get; }

    /// <summary>
    /// Logger
    /// </summary>
    [Autowired]
    protected ILogger Logger { get; }

    /// <summary>
    /// Db
    /// </summary>
    [Autowired]
    protected ISqlSugarClient Db { get; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [Autowired]
    public string PluginId { get; }

    /// <inheritdoc />
    public virtual int Priority => 0;

    /// <inheritdoc />
    public virtual async Task ExportComic(StorageFile outputItem, LocalComic comic, DispatcherQueue dispatcher,
        CancellationToken token)
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
            Title = I18N.ImportComic + ": " + comic.Name,
            Severity = InfoBarSeverity.Informational,
            IsClosable = false,
            IsIconVisible = true,
            IsOpen = true,
            FlowDirection = FlowDirection.LeftToRight,
            Content = progressStackPanel
        };
        NotifyService.NotifyTip(
            this, infoBar, 0, TipPopupPosition.Right);
        progressRing.IsIndeterminate = false;
        progressRingBackground.Visibility = Visibility.Visible;
        progressRingText.Visibility = Visibility.Visible;
        if (outputItem.FileType == ".zip")
        {
            await Task.Run(()=>  CreateZip(outputItem, comic, dispatcher, token,
                new Progress<double>(async void (v) =>
                {
                    try
                    {
                        await dispatcher.EnqueueAsync(async () =>
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
                })), token);
        }
    }

    /// <summary>
    /// 创建zip
    /// </summary>
    /// <param name="outputItem"></param>
    /// <param name="comic"></param>
    /// <param name="dispatcher"></param>
    /// <param name="token"></param>
    /// <param name="progress"></param>
    protected async Task CreateZip(StorageFile outputItem, LocalComic comic, DispatcherQueue dispatcher,
        CancellationToken token, IProgress<double>? progress = null)
    {
        using var archive = ZipArchive.Create();
        var count = comic.Count;
        var current = 0;
        foreach (var ep in await Db.Queryable<LocalEpisode>().Where(x => x.ComicId == comic.Id).ToArrayAsync())
        {
            foreach (var pic in await Db.Queryable<LocalPicture>()
                         .Where(x => x.ComicId == comic.Id && x.EpisodeId == ep.Id).ToArrayAsync())
            {
                archive.AddEntry($"{ep.Name}/{pic.Name}", pic.Img);
                current += 1;
                progress?.Report((double)current / count * 100); 
            }
        }

        archive.SaveTo(outputItem.Path, CompressionType.Deflate);
    }
    /// <inheritdoc />
    public virtual Dictionary<string, IList<string>> SupportTypes => new()
    {
        ["zip"] = new List<string> { ".zip" },
    };
}