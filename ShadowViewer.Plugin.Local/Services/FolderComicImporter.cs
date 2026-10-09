using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Sdk.Models;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Sdk.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Sdk.Services;
using ShadowViewer.Plugin.Local.I18n;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 文件夹类型导入器
/// </summary>
public partial class FolderComicImporter : IComicImporter
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [Autowired]
    public string PluginId { get; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [Autowired]
    public string Version { get; }

    /// <summary>
    /// NotifyService
    /// </summary>
    [Autowired]
    protected INotifyService NotifyService { get; }

    /// <inheritdoc/>
    public virtual string Name => "SingleComicFolder";

    /// <inheritdoc />
    public virtual int Priority => 0;

    /// <inheritdoc />
    public virtual string Description => I18N.SingleComicFolderImporterDescription;

    /// <summary>
    /// Logger
    /// </summary>
    [Autowired]
    protected ILogger Logger { get; }

    /// <summary>
    /// Db
    /// </summary>
    [Autowired]
    protected IDbContextFactory<LocalDbContext> DbFactory { get; }

    /// <summary>
    /// 保存漫画
    /// </summary>
    /// <param name="path"></param>
    /// <param name="comicId"></param>
    /// <param name="findThumb"></param>
    /// <returns></returns>
    protected async Task<ShadowTreeNode> SaveComic(string path, long comicId, bool findThumb = false, LocalDbContext? context = null)
    {
        using var ownedContext = context == null ? DbFactory.CreateDbContext() : null;
        var db = context ?? ownedContext!;
        var node = ShadowTreeNode.FromFolder(path);
        ShadowTreeNode? thumb = null;
        var number = 1;
        var pics = new List<ComicPicture>();
        var comicNode = node.GetFilesByHeight(2).FirstOrDefault();
        // 检查是否是多话漫画
        if (comicNode != null)
        {
            if (findThumb) thumb = comicNode.Children.FirstOrDefault(child => child.IsPic);

            foreach (var child in comicNode.Children.Where(child => child is { IsDirectory: true, Count: > 0 }))
            {
                pics.AddRange(await CreateEpisode(db, comicId, child, number));
                number++;
            }
        }
        else
        {
            var epNode = node.GetFilesByHeight(1).FirstOrDefault();
            if (epNode != null)
            {
                if (findThumb) thumb = epNode.Children.FirstOrDefault(child => child.IsPic);
                pics.AddRange(await CreateEpisode(db, comicId, epNode, number));
            }
        }

        if (thumb != null)
        {
            await db.Set<ComicNode>().Where(x => x.Id == comicId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Thumb, thumb.Path)
                    .SetProperty(x => x.UpdatedDateTime, DateTime.Now));
        }

        db.AddRange(pics);
        await db.SaveChangesAsync();
        var episodeCount =
            await db.Set<ComicChapter>().Where(x => x.ComicId == comicId).CountAsync();
        var count = await db.Set<ComicPicture>().Where(x => x.ComicId == comicId).CountAsync();
        await db.Set<ComicNode>().Where(x => x.Id == comicId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(it => it.Size, node.Size)
                .SetProperty(x => x.UpdatedDateTime, DateTime.Now));
        await db.Set<ComicDetail>().Where(x => x.ComicId == comicId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(it => it.ChapterCount, episodeCount)
                .SetProperty(it => it.PageCount, count));
        return node;
    }

    private async Task<IEnumerable<ComicPicture>> CreateEpisode(LocalDbContext db, long comicId,
        ShadowTreeNode child, int number)
    {
        var chapter = new ComicChapter
        {
            Name = child.Name,
            Order = number,
            ComicId = comicId,
            PageCount = child.Count,
            Size = child.Size,
            CreatedDateTime = DateTime.Now,
        };
        db.Add(chapter);
        await db.SaveChangesAsync();
        var epId = chapter.Id;
        return child.Children
            .Where(c => c.IsPic)
            .Select(item =>
                new ComicPicture
                {
                    Name = item.Name,
                    ChapterId = epId,
                    ComicId = comicId,
                    StoragePath = item.Path,
                    Size = item.Size,
                    CreatedDateTime = DateTime.Now,
                });
    }

    /// <inheritdoc />
    public virtual bool Check(IStorageItem item)
    {
        return item is StorageFolder;
    }

    /// <inheritdoc />
    public virtual async Task<ComicImportPreview> Preview(IStorageItem item)
    {
        if (item is not StorageFolder folder) return new ComicImportPreview();
        var node = ShadowTreeNode.FromFolder(folder.Path);
        var singlePreview = GetSingleComicPreview(node);
        singlePreview.SourceItem = folder;
        return singlePreview;
    }

    /// <summary>
    /// Gets the single comic preview.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns></returns>
    protected virtual ComicImportPreview GetSingleComicPreview(ShadowTreeNode node)
    {
        var preview = new ComicImportPreview
        {
            Name = node.Name,
            ComicDetail = new ComicDetail
            {
                StoragePath = node.Path
            }
        };

        var comicNode = node.GetFilesByHeight(2).FirstOrDefault();
        ShadowTreeNode? thumb = null;
        var episodeCount = 0;
        var count = 0;
        var chapters = new List<ComicChapter>();
        var imagesMap = new Dictionary<ComicChapter, List<ComicPicture>>();

        if (comicNode != null)
        {
            // Structure: Root -> Chapter Folders -> Images
            // comicNode is one of the nodes that has files at height 2.
            // But we should iterate all children that are folders.

            thumb = comicNode.Children.FirstOrDefault(child => child.IsPic);
            // Fallback for thumb if comicNode didn't have pic (unlikely if it was returned by GetFilesByHeight(2))

            var order = 1;
            foreach (var child in node.Children.Where(child => child is { IsDirectory: true, Count: > 0 }))
            {
                var chapterId = DatabaseIds.Next();
                var chapter = new ComicChapter()
                {
                    Id = chapterId,
                    Name = child.Name,
                    Order = order++,
                    CreatedDateTime = DateTime.Now,
                    // PageCount and Size calculated below
                };

                var pics = child.Children.Where(c => c.IsPic).Select(item => new ComicPicture
                {
                    Id = DatabaseIds.Next(),
                    Name = item.Name,
                    ChapterId = chapterId,
                    // ComicId set later
                    StoragePath = item.Path,
                    Size = item.Size,
                    CreatedDateTime = DateTime.Now,
                }).ToList();

                if (pics.Count > 0)
                {
                    chapter.PageCount = pics.Count;
                    chapter.Size = child.Size; // Or sum of pics size? using child.Size fits original logic

                    chapters.Add(chapter);
                    imagesMap[chapter] = pics;

                    count += pics.Count;
                    episodeCount++;
                }
            }
        }
        else
        {
            // Structure: Root -> Images
            // Single Chapter Mode
            var epNode = node.GetFilesByHeight(1).FirstOrDefault();
            if (epNode != null)
            {
                // In single chapter mode, "epNode" is effectively "node" usually?
                // Depending on GetFilesByHeight implementation. 
                // Original logic: epNode.Children...
                // If epNode is node, node.Children...

                thumb = epNode.Children.FirstOrDefault(child => child.IsPic);

                var chapterId = DatabaseIds.Next();
                var chapter = new ComicChapter()
                {
                    Id = chapterId,
                    Name = epNode.Name, // Or node.Name? Original used epNode.Name
                    Order = 1,
                    PageCount = epNode.Children.Count(c => c.IsPic),
                    Size = epNode.Size,
                    CreatedDateTime = DateTime.Now,
                };

                var pics = epNode.Children.Where(c => c.IsPic).Select(item => new ComicPicture
                {
                    Id = DatabaseIds.Next(),
                    Name = item.Name,
                    ChapterId = chapterId,
                    StoragePath = item.Path,
                    Size = item.Size,
                    CreatedDateTime = DateTime.Now,
                }).ToList();

                if (pics.Count > 0)
                {
                    chapter.PageCount = pics.Count;
                    chapters.Add(chapter);
                    imagesMap[chapter] = pics;

                    count += pics.Count;
                    episodeCount = 1;
                }
            }
        }

        preview.Thumb = thumb?.Path ?? "mx-appx:///default.png";
        preview.ComicDetail.ChapterCount = episodeCount;
        preview.ComicDetail.PageCount = count;
        preview.PreviewChapters = chapters;
        preview.PreviewImages = imagesMap;

        return preview;
    }

    /// <inheritdoc />
    public virtual async Task ImportComic(ComicImportPreview preview, long parentId, DispatcherQueue dispatcher,
        CancellationToken token, IProgress<double>? progress = null)
    {
        // Single import using preview info
        if (preview.SourceItem is StorageFolder || !string.IsNullOrEmpty(preview.ComicDetail.StoragePath))
        {
            await ImportDetail(preview, parentId);
            progress?.Report(100);
            NotifyService.NotifyTip(this, I18N.ImportComicSuccess, InfoBarSeverity.Success);
        }
    }

    /// <summary>
    /// Imports the detail.
    /// </summary>
    /// <param name="preview">The preview.</param>
    /// <param name="parentId">The parent identifier.</param>
    /// <returns></returns>
    protected virtual async Task ImportDetail(ComicImportPreview preview, long parentId)
    {
        using var db = DbFactory.CreateDbContext();
        using var transaction = await db.Database.BeginTransactionAsync();
        var path = preview.SourceItem?.Path ?? preview.ComicDetail.StoragePath;

        // Create ComicNode (The Book) with Detail attached
        var comic = new ComicNode()
        {
            Name = preview.Name,
            Thumb = preview.Thumb,
            ParentId = parentId,
            NodeType = "Comic",
            Size = preview.PreviewChapters.Sum(c => c.Size),
            ReadingRecord = new LocalReadingRecord()
            {
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            },
            ComicDetail = new ComicDetail()
            {
                ProcessMode = "Folder",
                StoragePath = path,
                ChapterCount = preview.ComicDetail.ChapterCount,
                PageCount = preview.ComicDetail.PageCount,
            },
            SourcePluginDataId = PluginId + Version
        };
        db.Add(comic);

        var comicId = comic.Id;

        // Prepare Entities for Bulk Insert
        var allChapters = preview.PreviewChapters;
        var allPictures = new List<ComicPicture>();

        foreach (var chapter in allChapters)
        {
            chapter.ComicId = comicId;
            // Chapter ID is already set in Preview
            if (preview.PreviewImages.TryGetValue(chapter, out var pics))
            {
                foreach (var pic in pics)
                {
                    pic.ComicId = comicId;
                    pic.ChapterId = chapter.Id; // Already mapped, but safe to ensure
                    allPictures.Add(pic);
                }
            }
        }

        // Bulk Insert
        if (allChapters.Count > 0)
        {
            db.AddRange(allChapters);
        }

        if (allPictures.Count > 0)
        {
            db.AddRange(allPictures);
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <inheritdoc />
    public virtual string[] SupportTypes => [];
}
