using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Core.Models;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using ShadowViewer.Core.Services;
using ShadowViewer.Plugin.Local.I18n;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 文件夹类型导入器
/// </summary>
public partial class FolderComicImporter : IComicImporter
{
    /// <summary>
    /// NotifyService
    /// </summary>
    [Autowired]
    protected INotifyService NotifyService { get; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [Autowired]
    public string PluginId { get; }

    /// <inheritdoc />
    public virtual int Priority => 0;

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
    /// 保存漫画
    /// </summary>
    /// <param name="path"></param>
    /// <param name="comicId"></param>
    /// <param name="findThumb"></param>
    /// <returns></returns>
    protected async Task<ShadowTreeNode> SaveComic(string path, long comicId, bool findThumb = false)
    {
        var node = ShadowTreeNode.FromFolder(path);
        ShadowTreeNode? thumb = null;
        var number = 1;
        var pics = new List<LocalPicture>();
        var comicNode = node.GetFilesByHeight(2).FirstOrDefault();
        // 检查是否是多话漫画
        if (comicNode != null)
        {
            if (findThumb) thumb = comicNode.Children.FirstOrDefault(child => child.IsPic);

            foreach (var child in comicNode.Children.Where(child => child is { IsDirectory: true, Count: > 0 }))
            {
                pics.AddRange(await CreateEpisode(comicId, child, number));
                number++;
            }
        }
        else
        {
            var epNode = node.GetFilesByHeight(1).FirstOrDefault();
            if (epNode != null)
            {
                if (findThumb) thumb = epNode.Children.FirstOrDefault(child => child.IsPic);
                pics.AddRange(await CreateEpisode(comicId, epNode, number));
            }
        }

        if (thumb != null)
        {
            await Db.Updateable<LocalComic>()
                .SetColumns(x => x.Thumb == thumb.Path)
                .Where(x => x.Id == comicId)
                .ExecuteCommandAsync();
        }

        await Db.Insertable(pics).ExecuteReturnSnowflakeIdListAsync();
        var episodeCount =
            await Db.Queryable<LocalEpisode>().Where(x => x.ComicId == comicId).CountAsync();
        var count = await Db.Queryable<LocalPicture>().Where(x => x.ComicId == comicId).CountAsync();
        await Db.Updateable<LocalComic>()
            .SetColumns(it => it.Size == node.Size)
            .SetColumns(it => it.EpisodeCount == episodeCount)
            .SetColumns(it => it.Count == count)
            .Where(x => x.Id == comicId)
            .ExecuteCommandAsync();
        return node;
    }

    private async Task<IEnumerable<LocalPicture>> CreateEpisode(long comicId,
        ShadowTreeNode child, int number)
    {
        var epId = await Db.Insertable(new LocalEpisode
        {
            Name = child.Name,
            Order = number,
            ComicId = comicId,
            PageCount = child.Count,
            Size = child.Size,
            CreateTime = DateTime.Now,
        }).ExecuteReturnSnowflakeIdAsync();
        return child.Children
            .Where(c => c.IsPic)
            .Select(item =>
                new LocalPicture
                {
                    Name = item.Name,
                    EpisodeId = epId,
                    ComicId = comicId,
                    Img = item.Path,
                    Size = item.Size,
                    CreateTime = DateTime.Now,
                });
    }

    /// <inheritdoc />
    public virtual bool Check(IStorageItem item)
    {
        return item is StorageFolder;
    }

    /// <inheritdoc />
    public virtual async Task ImportComic(IStorageItem item, long parentId, DispatcherQueue dispatcher,
        CancellationToken token)
    {
        if (item is not StorageFolder folder) return;
        var comic = await Db.InsertNav(new LocalComic()
        {
            Name = folder.DisplayName,
            Thumb = "mx-appx:///default.png",
            Affiliation = PluginId,
            ParentId = parentId,
            IsFolder = false,
            ReadingRecord = new LocalReadingRecord()
            {
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            },
        }).Include(z1 => z1.ReadingRecord).ExecuteReturnEntityAsync();
        await SaveComic(folder.Path, comic.Id, findThumb: true);
        NotifyService.NotifyTip(this, I18N.ImportComicSuccess, InfoBarSeverity.Success);
    }
}