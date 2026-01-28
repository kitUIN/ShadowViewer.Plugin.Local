using System;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Entities;

/// <summary>
/// 本地漫画-页
/// </summary>
public class ComicPicture
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
    public long ChapterId { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    [SugarColumn(ColumnDataType = "Nvarchar(2048)")]
    public string Name { get; set; } = null!;
    /// <summary>
    /// 图片地址
    /// </summary>
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "存储路径")]
    public string StoragePath { get; set; } = null!;
    /// <summary>
    /// 大小
    /// </summary>
    public long Size { get; set; }
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedDateTime { get; set; }

}