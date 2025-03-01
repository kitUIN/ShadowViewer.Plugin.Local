using System;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Models;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
/// <summary>
/// 本地漫画-页
/// </summary>
public class LocalPicture
{
    /// <summary>
    /// ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    public long Id { get; set; }
    /// <summary>
    /// 所属的漫画
    /// </summary>
    [SugarColumn()]
    public long ComicId { get; set; }
    /// <summary>
    /// 所属的漫画-话
    /// </summary>
    [SugarColumn()]
    public long EpisodeId { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    [SugarColumn(ColumnDataType = "Nvarchar(2048)")]
    public string Name { get; set; }
    /// <summary>
    /// 图片地址
    /// </summary>
    [SugarColumn(ColumnDataType = "Nvarchar(2048)")]
    public string Img { get; set; }
    /// <summary>
    /// 大小
    /// </summary>
    public long Size { get; set; }
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

}
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。