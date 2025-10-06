using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Controls.Extensions;
using ShadowViewer.Core.Cache;
using ShadowViewer.Core.Enums;
using ShadowViewer.Core.Extensions;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Plugin.Local.Cache;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SqlSugar;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using SharpCompress.Archives.Zip;
using CoreSettings = ShadowViewer.Core.Settings.CoreSettings;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 压缩包导入器
/// </summary>
[CheckAutowired]
public partial class ZipComicImporter : FolderComicImporter
{
    /// <summary>
    /// 支持的类型
    /// </summary>
    public override string[] SupportTypes => [".zip", ".rar", ".tar", ".cbr", ".cbz", ".shad"];


    /// <inheritdoc />
    public override bool Check(IStorageItem item)
    {
        return item is StorageFile file && SupportTypes.ContainsIgnoreCase(file.FileType);
    }


    /// <inheritdoc />
    public override async Task ImportComic(IStorageItem item, long parentId, DispatcherQueue dispatcher,
        CancellationToken token)
    {
        if (item is not StorageFile file) return;
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
            Title = I18N.ImportComic + ": " + file.DisplayName,
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
            passed = await Task.Run(() => CheckPassword(file.Path, op), token);
            if (passed) break;
            infoBar.Severity = InfoBarSeverity.Warning;
            infoBar.Title = I18N.NeedPassword + ": " + Path.GetFileNameWithoutExtension(file.Path);
            var dialog = XamlHelper.CreateOneTextBoxDialog(
                I18N.PasswordError,
                "", I18N.ZipPasswordPlaceholder, "",
                (_, _, text) => op.Password = text);
            var res = await DialogHelper.ShowDialog(dialog);
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
        await Task.Run(() => ImportComicFromZipAsync(file.Path,
            Core.Settings.CoreSettings.Instance.ComicsPath,
            PluginId, parentId,
            new Progress<MemoryStream>(async void (thumbStream) =>
            {
                try
                {
                    await dispatcher.EnqueueAsync(async () =>
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
            }), op), token);
    }


    /// <summary>
    /// 检测压缩包密码是否正确
    /// </summary>
    public async Task<bool> CheckPassword(string zip, ReaderOptions readerOptions)
    {
        var md5 = EncryptingHelper.CreateMd5(zip);
        var sha1 = EncryptingHelper.CreateSha1(zip);
        var cacheZip = await Db.Queryable<CacheZip>().FirstAsync(x => x.Sha1 == sha1 && x.Md5 == md5);
        if (cacheZip is { Password: not null } && cacheZip.Password != "")
        {
            readerOptions.Password = cacheZip.Password;
            Log.Information("自动填充密码:{Pwd}", cacheZip.Password);
        }

        try
        {
            await using var fStream = File.OpenRead(zip);
            await using var stream = NonDisposingStream.Create(fStream);
            using var archive = ArchiveFactory.Open(stream, readerOptions);
            await using var entryStream = archive.Entries.First(entry => !entry.IsDirectory).OpenEntryStream();
            // 密码正确添加压缩包密码存档
            // 能正常打开一个entry就代表正确,所以这个循环只走了一次
            await Db.Storageable(
                CacheZip.Create(md5, sha1, Path.GetFileNameWithoutExtension(zip),
                    password: readerOptions.Password)).ExecuteCommandAsync();

            return true;
        }
        catch (CryptographicException)
        {
            // 密码错误就删除压缩包密码存档
            await Db.Updateable<CacheZip>()
                .SetColumns(x => x.Password == null)
                .Where(x => x.Sha1 == sha1 && x.Md5 == md5)
                .ExecuteCommandAsync();
            return false;
        }
    }


    /// <summary>
    /// 解压压缩包并且导入
    /// </summary>
    /// <param name="zip"></param>
    /// <param name="destinationDirectory"></param>
    /// <param name="affiliation"></param>
    /// <param name="parentId"></param>
    /// <param name="thumbProgress"></param>
    /// <param name="progress"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    /// <exception cref="TaskCanceledException"></exception>
    public async Task<bool> ImportComicFromZipAsync(string zip,
        string destinationDirectory,
        string affiliation,
        long parentId,
        IProgress<MemoryStream>? thumbProgress = null,
        IProgress<double>? progress = null,
        ReaderOptions? readerOptions = null)
    {
        var comicId = SnowFlakeSingle.Instance.NextId();
        Logger.Information("进入{Zip}解压流程", zip);
        var path = Path.Combine(destinationDirectory, comicId.ToString());
        var md5 = EncryptingHelper.CreateMd5(zip);
        var sha1 = EncryptingHelper.CreateSha1(zip);
        var start = DateTime.Now;
        var cacheZip = await Db.Queryable<CacheZip>()
            .FirstAsync(x => x.Sha1 == sha1 && x.Md5 == md5);
        cacheZip ??= CacheZip.Create(md5, sha1, Path.GetFileNameWithoutExtension(zip));
        if (cacheZip.ComicId != null)
        {
            comicId = (long)cacheZip.ComicId;
            // 缓存文件未被删除
            if (Directory.Exists(cacheZip.CachePath))
            {
                var updateComicId = await Db.Updateable<LocalComic>()
                    .SetColumns(x => x.IsDelete == false)
                    .Where(x => x.Id == comicId)
                    .ExecuteCommandAsync();
                Logger.Information("{Zip}文件存在缓存记录,直接载入漫画{cid}", zip, cacheZip.ComicId);
                progress?.Report(100D);
                return true;
            }
        }

        await Db.InsertNav(new LocalComic()
            {
                Name = Path.GetFileNameWithoutExtension(zip),
                Thumb = "mx-appx:///default.png",
                Affiliation = affiliation,
                ParentId = parentId,
                IsFolder = false,
                Link = path,
                Id = comicId,
                ReadingRecord = new LocalReadingRecord()
                {
                    CreatedDateTime = DateTime.Now,
                    UpdatedDateTime = DateTime.Now,
                },
            })
            .Include(z1 => z1.ReadingRecord)
            .ExecuteCommandAsync();
        await using var fStream = File.OpenRead(zip);
        await using var stream = NonDisposingStream.Create(fStream);
        using var archive = ArchiveFactory.Open(stream, readerOptions);
        var total = archive.Entries.Where(
                entry => !entry.IsDirectory && (entry.Key?.IsPic() ?? false))
            .OrderBy(x => x.Key).ToList();
        var totalCount = total.Count;
        var ms = new MemoryStream();
        if (total.FirstOrDefault() is { } img)
        {
            await using (var entryStream = img.OpenEntryStream())
            {
                await entryStream.CopyToAsync(ms);
            }

            var bytes = ms.ToArray();
            CacheImg.CreateImage(CoreSettings.Instance.TempPath, bytes, comicId);
            thumbProgress?.Report(new MemoryStream(bytes));
        }

        Logger.Information("开始解压:{Zip}", zip);

        var i = 0;
        path.CreateDirectory();
        foreach (var entry in total)
        {
            entry.WriteToDirectory(path, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
            i++;
            var result = i / (double)totalCount;
            progress?.Report(Math.Round(result * 100, 2) - 0.01D);
        }

        var node = await SaveComic(path, comicId);

        progress?.Report(100D);
        var stop = DateTime.Now;
        cacheZip.ComicId = comicId;
        cacheZip.CachePath = path;
        cacheZip.Name = Path.GetFileNameWithoutExtension(zip)
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Last();
        await Db.Storageable(cacheZip).ExecuteCommandAsync();
        Logger.Information("解压成功:{Zip} 页数:{Pages} 耗时: {Time} s", zip, totalCount, (stop - start).TotalSeconds);
        //TODO 中断回滚
        return true;
    }
}