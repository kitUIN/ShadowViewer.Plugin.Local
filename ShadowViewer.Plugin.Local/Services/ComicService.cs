using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ShadowPluginLoader.MetaAttributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core;
using ShadowViewer.Core.Cache;
using ShadowViewer.Core.Extensions;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Models;
using ShadowViewer.Core.Services;
using ShadowViewer.Plugin.Local.Cache;
using ShadowViewer.Plugin.Local.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SqlSugar;
using ReaderOptions = SharpCompress.Readers.ReaderOptions;

namespace ShadowViewer.Plugin.Local.Services
{
    /// <summary>
    /// 
    /// </summary>
    public partial class ComicService
    {
        [Autowired] private ILogger Logger { get; }

        [Autowired] private ICallableService Caller { get; }
        [Autowired] private INotifyService NotifyService { get; }
        [Autowired] private ISqlSugarClient Db { get; }

        /// <summary>
        /// 检测压缩包密码是否正确
        /// </summary>
        public bool CheckPassword(string zip, ref ReaderOptions readerOptions)
        {
            var md5 = EncryptingHelper.CreateMd5(zip);
            var sha1 = EncryptingHelper.CreateSha1(zip);
            var cacheZip = Db.Queryable<CacheZip>().First(x => x.Sha1 == sha1 && x.Md5 == md5);
            if (cacheZip is { Password: not null } && cacheZip.Password != "")
            {
                readerOptions.Password = cacheZip.Password;
                Log.Information("自动填充密码:{Pwd}", cacheZip.Password);
            }

            try
            {
                using var fStream = File.OpenRead(zip);
                using var stream = NonDisposingStream.Create(fStream);
                using var archive = ArchiveFactory.Open(stream, readerOptions);
                using var entryStream = archive.Entries.First(entry => !entry.IsDirectory).OpenEntryStream();
                // 密码正确添加压缩包密码存档
                // 能正常打开一个entry就代表正确,所以这个循环只走了一次
                Db.Storageable(
                    CacheZip.Create(md5, sha1, Path.GetFileNameWithoutExtension(zip),
                        password: readerOptions.Password)).ExecuteCommand();

                return true;
            }
            catch (CryptographicException)
            {
                // 密码错误就删除压缩包密码存档
                Db.Updateable<CacheZip>()
                    .SetColumns(x => x.Password == null)
                    .Where(x => x.Sha1 == sha1 && x.Md5 == md5)
                    .ExecuteCommand();
                return false;
            }
        }

        public async Task ImportComicFromFolderAsync(string folder,
            string affiliation,
            long parentId,
            CancellationToken token,
            IProgress<MemoryStream>? thumbProgress = null,
            IProgress<double>? progress = null)
        {
            var comicId = await Db.Insertable(new LocalComic()
            {
                Name = Path.GetFileNameWithoutExtension(folder),
                Thumb = "mx-appx:///default.png",
                Affiliation = affiliation,
                ParentId = parentId,
                IsFolder = false
            }).ExecuteReturnSnowflakeIdAsync(token);
            await SaveComic(token, folder, comicId);
        }

        /// <summary>
        /// 解压压缩包并且导入
        /// </summary>
        /// <param name="zip"></param>
        /// <param name="destinationDirectory"></param>
        /// <param name="affiliation"></param>
        /// <param name="parentId"></param>
        /// <param name="thumbProgress"></param>
        /// <param name="token"></param>
        /// <param name="progress"></param>
        /// <param name="readerOptions"></param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException"></exception>
        public async Task<bool> ImportComicFromZipAsync(string zip,
            string destinationDirectory,
            string affiliation,
            long parentId,
            CancellationToken token,
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
                .FirstAsync(x => x.Sha1 == sha1 && x.Md5 == md5, token);
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
                        .ExecuteCommandAsync(token);
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
                    ReadingRecord = new LocalReadingRecord(),
                })
                .Include(z1 => z1.ReadingRecord)
                .ExecuteCommandAsync();
            await using var fStream = File.OpenRead(zip);
            await using var stream = NonDisposingStream.Create(fStream);
            if (token.IsCancellationRequested) throw new TaskCanceledException();
            using var archive = ArchiveFactory.Open(stream, readerOptions);
            if (token.IsCancellationRequested) throw new TaskCanceledException();
            var total = archive.Entries.Where(
                    entry => !entry.IsDirectory && (entry.Key?.IsPic() ?? false))
                .OrderBy(x => x.Key).ToList();
            if (token.IsCancellationRequested) throw new TaskCanceledException();
            var totalCount = total.Count;
            var ms = new MemoryStream();
            if (total.FirstOrDefault() is { } img)
            {
                await using (var entryStream = img.OpenEntryStream())
                {
                    await entryStream.CopyToAsync(ms, token);
                }

                var bytes = ms.ToArray();
                CacheImg.CreateImage(CoreSettings.TempPath, bytes, comicId);
                thumbProgress?.Report(new MemoryStream(bytes));
            }

            Logger.Information("开始解压:{Zip}", zip);

            var i = 0;
            path.CreateDirectory();
            foreach (var entry in total)
            {
                if (token.IsCancellationRequested) throw new TaskCanceledException();
                entry.WriteToDirectory(path, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                i++;
                var result = i / (double)totalCount;
                progress?.Report(Math.Round(result * 100, 2) - 0.01D);
            }

            var node = await SaveComic(token, path, comicId);
            var episodeCount =
                await Db.Queryable<LocalEpisode>().Where(x => x.ComicId == comicId).CountAsync(token);
            var count = await Db.Queryable<LocalPicture>().Where(x => x.ComicId == comicId).CountAsync(token);
            await Db.Updateable<LocalComic>()
                .SetColumns(it => it.Size == node.Size)
                .SetColumns(it => it.EpisodeCount == episodeCount)
                .SetColumns(it => it.Count == count)
                .Where(x => x.Id == comicId)
                .ExecuteCommandAsync(token);
            progress?.Report(100D);
            var stop = DateTime.Now;
            cacheZip.ComicId = comicId;
            cacheZip.CachePath = path;
            cacheZip.Name = Path.GetFileNameWithoutExtension(zip)
                .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Last();
            await Db.Storageable(cacheZip).ExecuteCommandAsync(token);
            Logger.Information("解压成功:{Zip} 页数:{Pages} 耗时: {Time} s", zip, totalCount, (stop - start).TotalSeconds);
            return true;
        }

        /// <summary>
        /// 保存漫画
        /// </summary>
        /// <param name="token"></param>
        /// <param name="path"></param>
        /// <param name="comicId"></param>
        /// <returns></returns>
        private async Task<ShadowTreeNode> SaveComic(CancellationToken token, string path, long comicId)
        {
            var node = ShadowTreeNode.FromFolder(path);
            var number = 1;
            var pics = new List<LocalPicture>();
            var comicNode = node.GetFilesByHeight(2).FirstOrDefault();
            // 检查是否是多话漫画
            if (comicNode != null)
            {
                foreach (var child in comicNode.Children.Where(child => child.IsDirectory))
                {
                    pics.AddRange(await CreateEpisode(token, comicId, child, number, pics));
                    number++;
                }
            }
            else
            {
                var epNode = node.GetFilesByHeight(1).FirstOrDefault();
                if (epNode != null)
                {
                    pics.AddRange(await CreateEpisode(token, comicId, epNode, number, pics));
                }
            }

            await Db.Insertable(pics).ExecuteReturnSnowflakeIdListAsync(token);
            return node;
        }

        private async Task<IEnumerable<LocalPicture>> CreateEpisode(CancellationToken token, long comicId,
            ShadowTreeNode child, int number, List<LocalPicture> pics)
        {
            var epId = await Db.Insertable(new LocalEpisode
            {
                Name = child.Name,
                Order = number,
                ComicId = comicId,
                PageCount = child.Count,
                Size = child.Size,
                CreateTime = DateTime.Now,
            }).ExecuteReturnSnowflakeIdAsync(token);
            return child.Children
                .Where(c => !c.IsDirectory)
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
    }
}