using DryIoc;
using Serilog;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Plugin.Local.Models;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Cache
{
    /// <summary>
    /// 缓存的临时缩略图
    /// </summary>
    public class CacheImg
    {
        public CacheImg() { }

        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        [SugarColumn(ColumnDataType = "Nchar(32)", IsNullable = false)]
        public string Md5 { get; set; }
        [SugarColumn(ColumnDataType = "Ntext")]
        public string Path { get; set; }
        /// <summary>
        /// 标签
        /// </summary>
        [SugarColumn()]
        public long ComicId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="bytes"></param>
        /// <param name="comicId"></param>
        public static void CreateImage(string dir, byte[] bytes, long comicId)
        {
            var db = DiFactory.Services.Resolve<ISqlSugarClient>();
            var md5 = EncryptingHelper.CreateMd5(bytes);
            var path = System.IO.Path.Combine(dir, md5 + ".png");
            System.IO.File.WriteAllBytes(path, bytes);
            if (db.Queryable<CacheImg>().First(x => x.Md5 == md5) is { } cache)
            {
                db.Insertable(new CacheImg
                {
                    Md5 = md5,
                    ComicId = cache.ComicId,
                    Path = cache.Path,
                }).ExecuteReturnIdentity();
                db.Updateable<LocalComic>()
                    .SetColumns(it => it.Thumb == cache.Path)
                    .Where(x=>x.Id == comicId)
                    .ExecuteCommand();
            }
            else
            {
                db.Insertable(new CacheImg
                {
                    Md5 = md5,
                    Path = path,
                    ComicId = comicId,
                }).ExecuteReturnIdentity();
                db.Updateable<LocalComic>()
                    .SetColumns(it => it.Thumb == path)
                    .Where(x => x.Id == comicId)
                    .ExecuteCommand();
            }
        }

        [SugarColumn(IsIgnore = true)]
        public static ILogger Logger { get; } = Log.ForContext<CacheImg>();
    }
}
