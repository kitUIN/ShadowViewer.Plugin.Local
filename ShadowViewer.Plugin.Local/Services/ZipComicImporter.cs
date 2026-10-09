using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowPluginLoader.WinUI.Config;
using ShadowViewer.Plugin.Local.Cache;
using ShadowViewer.Plugin.Local.Configs;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk.Cache;
using ShadowViewer.Sdk.Extensions;
using ShadowViewer.Sdk.Helpers;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Sdk.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using ShadowViewer.Plugin.Local.Entities;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 压缩包导入器
/// </summary>
[CheckAutowired]
public partial class ZipComicImporter : FolderComicImporter
{
    [Autowired] private BaseSdkConfig BaseSdkConfig { get; }
    [Autowired] private LocalPluginConfig LocalPluginConfig { get; }

    /// <summary>
    /// 手动输入的密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 支持的类型
    /// </summary>
    public override string[] SupportTypes => [".zip", ".rar", ".tar", ".cbr", ".cbz", ".shad", ".7z"];

    /// <inheritdoc />
    public override string Name => "ZipToFolder";

    /// <inheritdoc />
    public override string Description => I18N.ZipImporterDescription;

    /// <inheritdoc />
    public override bool Check(IStorageItem item)
    {
        return item is StorageFile file && SupportTypes.ContainsIgnoreCase(file.FileType);
    }

    /// <inheritdoc />
    public override async Task<ComicImportPreview> Preview(IStorageItem item)
    {
        if (item is not StorageFile file) return new ComicImportPreview();
        var op = new ReaderOptions();
        if (!string.IsNullOrEmpty(Password))
        {
            op.Password = Password;
        }
        var passed = await Task.Run(() => CheckPassword(file.Path, op));
        
        if (!passed) return new ComicImportPreview()
        {
            Name = file.DisplayName, 
            SourceItem = file, 
            IsPasswordRequired = true
        };

        return await Task.Run(async () =>
        {
            try
            {
                using var archive = ArchiveFactory.Open(file.Path, op);
                var validEntries = archive.Entries
                    .Where(entry => !entry.IsDirectory && (entry.Key?.IsPic() ?? false))
                    .OrderBy(x => x.Key)
                    .Select(e => new { Entry = e, Key = e.Key?.Replace('\\', '/').TrimStart('/') })
                    .ToList();

                if (validEntries.Count == 0) return new ComicImportPreview() { Name = file.DisplayName, SourceItem = file };
                
                var count = validEntries.Count;
                var thumb = "mx-appx:///default.png";
                if (validEntries.FirstOrDefault() is { Entry: {} img })
                {
                    var tempPath = Path.Combine(BaseSdkConfig.TempFolderPath, Guid.NewGuid() + ".jpg");
                    await using (var entryStream = img.OpenEntryStream())
                    {
                        await using var fs = File.Create(tempPath);
                        await entryStream.CopyToAsync(fs);
                    }
                    thumb = tempPath;
                }

                // Parse Chapters
                // Detect Common Root
                string? commonRoot = null;
                if (validEntries.Count > 0)
                {
                    var firstKey = validEntries[0].Key;
                    if (firstKey != null)
                    {
                        var slashIndex = firstKey.IndexOf('/');
                        if (slashIndex != -1)
                        {
                            var checkRoot = firstKey[..(slashIndex + 1)];
                            if (validEntries.All(x => x.Key!.StartsWith(checkRoot, StringComparison.OrdinalIgnoreCase)))
                            {
                                commonRoot = checkRoot;
                            }
                        }
                    }
                }

                var processedEntries = validEntries.Select(x => new 
                { 
                    x.Entry, 
                    OriginalKey = x.Key, 
                    RelativeKey = (commonRoot != null && x.Key!.Length > commonRoot.Length) 
                                  ? x.Key[commonRoot.Length..] 
                                  : x.Key
                }).ToList();

                bool isMulti = processedEntries.Any(x => x.RelativeKey!.Contains('/'));
                var grouped = processedEntries.GroupBy(x => 
                {
                   if (!isMulti) return "";
                   var idx = x.RelativeKey!.IndexOf('/');
                   return idx < 0 ? "" : x.RelativeKey.Substring(0, idx);
                });

                var chapters = new List<ComicChapter>();
                var imagesMap = new Dictionary<ComicChapter, List<ComicPicture>>();
                int order = 1;
                foreach(var g in grouped)
                {
                    var chapterName = isMulti && !string.IsNullOrEmpty(g.Key) ? g.Key : file.DisplayName; 
                    if (isMulti && string.IsNullOrEmpty(g.Key)) continue;

                    var chapterId = DatabaseIds.Next();
                    var chapter = new ComicChapter()
                    {
                        Id = chapterId,
                        Name = chapterName,
                        Order = order++,
                        PageCount = g.Count(),
                        CreatedDateTime = DateTime.Now
                    };
                    
                    var pics = new List<ComicPicture>();
                    foreach(var item in g)
                    {
                        pics.Add(new ComicPicture()
                        {
                            Id = DatabaseIds.Next(),
                            Name = Path.GetFileName(item.OriginalKey)!,
                            ChapterId = chapterId,
                            StoragePath = item.OriginalKey!, // Provisional path (relative)
                            Size = item.Entry.Size,
                            CreatedDateTime = DateTime.Now
                        });
                    }
                    chapter.Size = pics.Sum(p => p.Size);
                    chapters.Add(chapter);
                    imagesMap[chapter] = pics;
                }

                return new ComicImportPreview()
                {
                    Name = file.DisplayName,
                    Thumb = thumb,
                    ComicDetail = new ComicDetail()
                    {
                        ChapterCount = chapters.Count,
                        PageCount = count
                    },
                    SourceItem = file,
                    Password = op.Password,
                    PreviewChapters = chapters,
                    PreviewImages = imagesMap
                };
            }
            catch (Exception e)
            {
                Logger.Error("预览压缩包失败: {e}", e);
                return new ComicImportPreview() { Name = file.DisplayName, SourceItem = file };
            }
        });
    }

    /// <inheritdoc />
    public override async Task ImportComic(ComicImportPreview preview, long parentId, DispatcherQueue dispatcher,
        CancellationToken token, IProgress<double>? progress = null)
    {
        if (preview.SourceItem is not StorageFile file) return;
        
        var op = new ReaderOptions();
        if (!string.IsNullOrEmpty(preview.Password))
        {
            op.Password = preview.Password;
        }
        await Task.Run(() => ImportComicFromZipAsync(preview, file.Path,
            LocalPluginConfig.ComicFolderPath,
            PluginId, parentId,
            new Progress<MemoryStream>(async void (thumbStream) =>
            {
               // Original code updated zipThumb.Source. We don't have zipThumb anymore.
               // We can ignore thumb progress or expose it? User didn't ask for thumb update in dialog.
            }),
            progress, op), token);
    }


    /// <summary>
    /// 检测压缩包密码是否正确
    /// </summary>
    public async Task<bool> CheckPassword(string zip, ReaderOptions readerOptions)
    {
        using var db = DbFactory.CreateDbContext();
        var md5 = EncryptingHelper.CreateMd5(zip);
        var sha1 = EncryptingHelper.CreateSha1(zip);
        var cacheZip = await db.Set<CacheZip>().FirstOrDefaultAsync(x => x.Sha1 == sha1 && x.Md5 == md5);
        if (cacheZip is { Password: not null } && cacheZip.Password != "")
        {
            readerOptions.Password = cacheZip.Password;
            Log.Information("自动填充密码:{Pwd}", cacheZip.Password);
        }

        try
        {
            await using var fStream = File.OpenRead(zip);
            using var archive = ArchiveFactory.Open(fStream, readerOptions);
            await using var entryStream = archive.Entries.First(entry => !entry.IsDirectory).OpenEntryStream();
            // 密码正确添加压缩包密码存档
            // 能正常打开一个entry就代表正确,所以这个循环只走了一次
            if (cacheZip == null)
            {
                cacheZip = CacheZip.Create(md5, sha1, Path.GetFileNameWithoutExtension(zip));
                db.Add(cacheZip);
            }
            cacheZip.Password = readerOptions.Password;
            await db.SaveChangesAsync();

            return true;
        }
        catch (CryptographicException)
        {
            // 密码错误就删除压缩包密码存档
            await db.Set<CacheZip>().Where(x => x.Sha1 == sha1 && x.Md5 == md5)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Password, (string?)null));
            return false;
        }
    }


    /// <summary>
    /// 解压压缩包并且导入
    /// </summary>
    /// <param name="preview"></param>
    /// <param name="zip"></param>
    /// <param name="destinationDirectory"></param>
    /// <param name="affiliation"></param>
    /// <param name="parentId"></param>
    /// <param name="thumbProgress"></param>
    /// <param name="progress"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    /// <exception cref="TaskCanceledException"></exception>
    public async Task<bool> ImportComicFromZipAsync(ComicImportPreview preview, string zip,
        string destinationDirectory,
        string affiliation,
        long parentId,
        IProgress<MemoryStream>? thumbProgress = null,
        IProgress<double>? progress = null,
        ReaderOptions? readerOptions = null)
    {
        using var db = DbFactory.CreateDbContext();
        var comicId = DatabaseIds.Next();
        Logger.Information("进入{Zip}解压流程", zip);
        var path = Path.Combine(destinationDirectory, comicId.ToString());
        var md5 = EncryptingHelper.CreateMd5(zip);
        var sha1 = EncryptingHelper.CreateSha1(zip);
        var start = DateTime.Now;
        var cacheZip = await db.Set<CacheZip>()
            .FirstOrDefaultAsync(x => x.Sha1 == sha1 && x.Md5 == md5);
        if (cacheZip == null)
        {
            cacheZip = CacheZip.Create(md5, sha1, Path.GetFileNameWithoutExtension(zip));
            db.Add(cacheZip);
        }
        if (cacheZip.ComicId != null)
        {
            comicId = (long)cacheZip.ComicId;
            // 缓存文件未被删除
            if (Directory.Exists(cacheZip.CachePath))
            {
                await db.Set<ComicNode>().Where(x => x.Id == comicId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.IsDelete, false)
                        .SetProperty(x => x.UpdatedDateTime, DateTime.Now));
                Logger.Information("{Zip}文件存在缓存记录,直接载入漫画{cid}", zip, cacheZip.ComicId);
                progress?.Report(100D);
                return true;
            }
        }
        
        path = Path.Combine(destinationDirectory, comicId.ToString());
        await using var fStream = File.OpenRead(zip);
        using var archive = ArchiveFactory.Open(zip, readerOptions);
        var total = archive.Entries.Where(entry => !entry.IsDirectory && (entry.Key?.IsPic() ?? false))
            .OrderBy(x => x.Key).ToList();
        var totalCount = total.Count;
        using var ms = new MemoryStream();
        if (total.FirstOrDefault() is { } img)
        {
            await using (var entryStream = img.OpenEntryStream())
            {
                await entryStream.CopyToAsync(ms);
            }

            var bytes = ms.ToArray();
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

        // File extraction finishes before taking a SQLite write lock.
        using var transaction = await db.Database.BeginTransactionAsync();
        var comicNode = await db.Set<ComicNode>().Include(x => x.ReadingRecord).Include(x => x.ComicDetail)
            .FirstOrDefaultAsync(x => x.Id == comicId);
        if (comicNode == null)
        {
            comicNode = new ComicNode
            {
                Id = comicId,
                Name = Path.GetFileNameWithoutExtension(zip),
                NodeType = "Comic",
                SourcePluginDataId = PluginId + Version,
                ReadingRecord = new LocalReadingRecord(),
                ComicDetail = new ComicDetail()
            };
            db.Add(comicNode);
        }
        else
        {
            // Rebuild missing archive files while retaining tags, authors, remarks and reading progress.
            await db.Set<ComicChapter>().Where(x => x.ComicId == comicId).ExecuteDeleteAsync();
            await db.Set<ComicPicture>().Where(x => x.ComicId == comicId).ExecuteDeleteAsync();
            comicNode.ReadingRecord ??= new LocalReadingRecord();
            comicNode.ComicDetail ??= new ComicDetail();
        }
        comicNode.ParentId = parentId;
        comicNode.IsDelete = false;
        comicNode.ComicDetail!.ProcessMode = "Zip";
        comicNode.ComicDetail.StoragePath = path;
        comicNode.ComicDetail.ChapterCount = preview.ComicDetail.ChapterCount;
        comicNode.ComicDetail.PageCount = preview.ComicDetail.PageCount;
        await db.SaveChangesAsync();
        if (ms.Length > 0)
            CacheImg.CreateImage(BaseSdkConfig.TempFolderPath, ms.ToArray(), comicId, db);

        if (preview.PreviewChapters.Count > 0)
        {
            foreach (var (chapter, pictures) in preview.PreviewImages)
            {
                chapter.ComicId = comicId;
                foreach (var picture in pictures)
                {
                    picture.ComicId = comicId;
                    picture.StoragePath = Path.Combine(path, picture.StoragePath);
                }
            }
            var allPictures = preview.PreviewImages.Values.SelectMany(x => x).ToList();
            db.AddRange(preview.PreviewChapters);
            db.AddRange(allPictures);
            await db.SaveChangesAsync();
            comicNode.Size = preview.PreviewChapters.Sum(x => x.Size);
            if (allPictures.FirstOrDefault() is { } firstPicture)
                comicNode.Thumb = firstPicture.StoragePath;
        }
        else
        {
            await SaveComic(path, comicId, context: db);
        }

        progress?.Report(100D);
        var stop = DateTime.Now;
        cacheZip.ComicId = comicId;
        cacheZip.CachePath = path;
        cacheZip.Name = Path.GetFileNameWithoutExtension(zip)
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Last();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Logger.Information("解压成功:{Zip} 页数:{Pages} 耗时: {Time} s", zip, totalCount, (stop - start).TotalSeconds);
        return true;
    }
}
