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
using SqlSugar;
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

                    var chapterId = SnowFlakeSingle.Instance.NextId();
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
                            Id = SnowFlakeSingle.Instance.NextId(),
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
            using var archive = ArchiveFactory.Open(fStream, readerOptions);
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
                await Db.Updateable<LocalComic>()
                    .SetColumns(x => x.IsDelete == false)
                    .Where(x => x.Id == comicId)
                    .ExecuteCommandAsync();
                Logger.Information("{Zip}文件存在缓存记录,直接载入漫画{cid}", zip, cacheZip.ComicId);
                progress?.Report(100D);
                return true;
            }
        }
        
        // 1. Init Comic Node and Detail from preview if possible
        var comicNode = new ComicNode()
        {
            Name = Path.GetFileNameWithoutExtension(zip),
            Thumb = "mx-appx:///default.png",
            ParentId = parentId,
            NodeType = "Comic",
            Id = comicId,
            ReadingRecord = new LocalReadingRecord() { CreatedDateTime = DateTime.Now, UpdatedDateTime = DateTime.Now },
            ComicDetail = new ComicDetail() { ComicId = comicId, StoragePath = path },
            SourcePluginDataId = PluginId + Version
        };

        if (preview?.ComicDetail != null)
        {
            comicNode.ComicDetail.ChapterCount = preview.ComicDetail.ChapterCount;
            comicNode.ComicDetail.PageCount = preview.ComicDetail.PageCount;
        }

        await Db.InsertNav(comicNode)
            .Include(z1 => z1.ReadingRecord)
            .Include(z1 => z1.ComicDetail)
            .ExecuteCommandAsync();
        
        await using var fStream = File.OpenRead(zip);
        using var archive = ArchiveFactory.Open(zip, readerOptions);
        var total = archive.Entries.Where(entry => !entry.IsDirectory && (entry.Key?.IsPic() ?? false))
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
            CacheImg.CreateImage(BaseSdkConfig.TempFolderPath, bytes, comicId);
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

        // 2. Use Preview Content for Insert if valid
        if (preview != null && preview.PreviewChapters.Count > 0)
        {
             // Fix Paths
             foreach(var kvp in preview.PreviewImages)
             {
                 foreach(var pic in kvp.Value)
                 {
                     pic.ComicId = comicId;
                     // Convert relative zip path to absolute path
                     // Zip Entry Key usually has backslashes or forward slashes.
                     // The extraction with ExtractFullPath = true preserves structure.
                     // On Windows, paths will use backslash if SharpCompress respects OS, 
                     // but usually we can join paths.
                     // The preview.Key was user saved earlier as `item.Key`.
                     // `item.Key` usually is strict relative path in Zip.
                     // `WriteToDirectory` uses `Path.Combine(path, entry.Key)`.
                     // So:
                     pic.StoragePath = Path.Combine(path, pic.StoragePath);
                 }
                 kvp.Key.ComicId = comicId;
             }
             
             // Bulk Insert
             var allPics = preview.PreviewImages.Values.SelectMany(x => x).ToList();
             await Db.Insertable(preview.PreviewChapters).ExecuteCommandAsync();
             await Db.Insertable(allPics).ExecuteCommandAsync();
             
             // Update Thumb (ComicNode)
             if (!string.IsNullOrEmpty(preview.Thumb) && !preview.Thumb.StartsWith("mx-appx"))
             {
                  // The thumb in preview is a temp file. We might want to point to the real file in the comic folder if possible.
                  // But usually preview thumb is fine as long as temp isn't deleted immediately?
                  // Better: Find the thumb in the extracted files.
                  var firstPic = allPics.FirstOrDefault();
                  if (firstPic != null)
                  {
                       await Db.Updateable<ComicNode>().SetColumns(x => x.Thumb == firstPic.StoragePath).Where(x => x.Id == comicId).ExecuteCommandAsync();
                  }
             }
        }
        else
        {
            await SaveComic(path, comicId);
        }

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