using DryIoc;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Sdk.Helpers;
using SqlSugar;

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
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// MD5
        /// </summary>
        [SugarColumn(ColumnDataType = "Nchar(32)", IsNullable = false)]
        public string Md5 { get; set; } = null!;

        /// <summary>
        /// 文件夹
        /// </summary>
        [SugarColumn(ColumnDataType = "Text")]
        public string Dir { get; set; } = null!;

        /// <summary>
        /// 路径
        /// </summary>
        public string Path => System.IO.Path.Combine(Dir, $"{Id}.png");

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
            var id = SnowFlakeSingle.Instance.NextId();
            var path = System.IO.Path.Combine(dir, $"{id}.png");
            System.IO.File.WriteAllBytes(path, bytes);
            if (db.Queryable<CacheImg>().First(x => x.Md5 == md5) is { } cache)
            {
                db.Updateable<ComicNode>()
                    .SetColumns(it => it.Thumb == cache.Path)
                    .Where(x => x.Id == comicId)
                    .ExecuteCommand();
            }
            else
            {
                db.Insertable(new CacheImg
                {
                    Id = id,
                    Md5 = md5,
                    Dir = dir,
                    ComicId = comicId,
                }).ExecuteCommand();
                db.Updateable<ComicNode>()
                    .SetColumns(it => it.Thumb == path)
                    .Where(x => x.Id == comicId)
                    .ExecuteCommand();
            }
        }
    }
}