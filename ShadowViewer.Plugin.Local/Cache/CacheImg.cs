using System.Linq;
using System;
using DryIoc;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Sdk.Helpers;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Sdk.Database;

namespace ShadowViewer.Plugin.Local.Cache
{
    /// <summary>
    /// 缓存的临时缩略图
    /// </summary>
    public class CacheImg
    {
        /// <summary>
        /// Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// MD5
        /// </summary>
        public string Md5 { get; set; } = null!;

        /// <summary>
        /// 文件夹
        /// </summary>
        public string Dir { get; set; } = null!;

        /// <summary>
        /// 路径
        /// </summary>
        public string Path => System.IO.Path.Combine(Dir, $"{Id}.png");

        /// <summary>
        /// 标签
        /// </summary>
        public long ComicId { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="bytes"></param>
        /// <param name="comicId"></param>
        public static void CreateImage(string dir, byte[] bytes, long comicId, LocalDbContext? context = null)
        {
            using var ownedContext = context == null
                ? DiFactory.Services.Resolve<IDbContextFactory<LocalDbContext>>().CreateDbContext() : null;
            var db = context ?? ownedContext!;
            var md5 = EncryptingHelper.CreateMd5(bytes);
            var id = DatabaseIds.Next();
            var path = System.IO.Path.Combine(dir, $"{id}.png");

            if (db.Set<CacheImg>().FirstOrDefault(x => x.Md5 == md5) is { } cache)
            {
                db.Set<ComicNode>().Where(x => x.Id == comicId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(it => it.Thumb, cache.Path)
                        .SetProperty(x => x.UpdatedDateTime, DateTime.Now));
            }
            else
            {
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(path, bytes);
                db.Add(new CacheImg
                {
                    Id = id,
                    Md5 = md5,
                    Dir = dir,
                    ComicId = comicId,
                });
                db.SaveChanges();
                db.Set<ComicNode>().Where(x => x.Id == comicId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(it => it.Thumb, path)
                        .SetProperty(x => x.UpdatedDateTime, DateTime.Now));
            }
        }
    }
}
