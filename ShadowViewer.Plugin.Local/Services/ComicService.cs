using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ShadowPluginLoader.MetaAttributes;
using ShadowViewer.Core;
using ShadowViewer.Core.Cache;
using ShadowViewer.Core.Extensions;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Models;
using ShadowViewer.Core.Services;
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
        [Autowired] private ISqlSugarClient Db { get; }

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
        public async Task<object> DeCompressImportAsync(string zip,
            string destinationDirectory,
            string affiliation,
            long parentId,
            CancellationToken token,
            IProgress<MemoryStream>? thumbProgress = null,
            IProgress<double>? progress = null,
            ReaderOptions? readerOptions = null)
        {
            var comicId = await Db.Insertable(new LocalComic()
            {
                Name = Path.GetFileNameWithoutExtension(zip),
                Thumb = "mx-appx:///default.png",
                Affiliation = affiliation,
                ParentId = parentId,
                IsFolder = false
            }).ExecuteReturnSnowflakeIdAsync(token);
            Logger.Information("进入{Zip}解压流程", zip);
            var path = Path.Combine(destinationDirectory, comicId.ToString());
            var md5 = EncryptingHelper.CreateMd5(zip);
            var sha1 = EncryptingHelper.CreateSha1(zip);
            var start = DateTime.Now;
            var cacheZip = await Db.Queryable<CacheZip>().FirstAsync(x => x.Sha1 == sha1 && x.Md5 == md5, token);
            cacheZip ??= CacheZip.Create(md5, sha1);
            if (cacheZip.ComicId != null)
            {
                comicId = (long)cacheZip.ComicId;
                path = Path.Combine(destinationDirectory, comicId.ToString());
                // 缓存文件未被删除
                if (Directory.Exists(cacheZip.CachePath))
                {
                    Logger.Information("{Zip}文件存在缓存记录,直接载入漫画{cid}", zip, cacheZip.ComicId);
                    return cacheZip;
                }
            }

            await using var fStream = File.OpenRead(zip);
            await using var stream = NonDisposingStream.Create(fStream);
            if (token.IsCancellationRequested) throw new TaskCanceledException();
            using var archive = ArchiveFactory.Open(stream, readerOptions);
            if (token.IsCancellationRequested) throw new TaskCanceledException();
            var total = archive.Entries.Where(
                    entry => !entry.IsDirectory && (entry.Key?.IsPic() ?? false))
                .OrderBy(x => x.Key);
            if (token.IsCancellationRequested) throw new TaskCanceledException();
            var totalCount = total.Count();
            var ms = new MemoryStream();
            if (total.FirstOrDefault() is { } img)
            {
                await using (var entryStream = img.OpenEntryStream())
                {
                    await entryStream.CopyToAsync(ms, token);
                }

                var bytes = ms.ToArray();
                CacheImg.CreateImage(CoreSettings.TempPath, bytes, comicId.ToString());
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
            var stop = DateTime.Now;
            cacheZip.ComicId = comicId;
            cacheZip.CachePath = path;
            cacheZip.Name = Path.GetFileNameWithoutExtension(zip)
                .Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last();
            await Db.Storageable(cacheZip).ExecuteCommandAsync(token);
            Logger.Information("解压成功:{Zip} 页数:{Pages} 耗时: {Time} s", zip, totalCount, (stop - start).TotalSeconds);
            return node;
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

        private async Task<IEnumerable<LocalPicture>> CreateEpisode(CancellationToken token, long comicId, ShadowTreeNode child, int number, List<LocalPicture> pics)
        {
            var epId = await Db.Insertable(new LocalEpisode
            {
                Name = child.Name,
                Order = number,
                ComicId = comicId,
                PageCounts = child.Count,
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