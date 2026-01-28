using ShadowViewer.Plugin.Local.Models;
using SqlSugar;
using System.Collections.Generic;

namespace ShadowViewer.Plugin.Local.Entities;

/// <summary>
/// 漫画专属信息（一对一对应ComicNode）
/// </summary>
public class ComicDetail
{
    /// <summary>
    /// 漫画Id (对应 ComicNode.Id)
    /// </summary>

    [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    public long ComicId { get; set; }

    /// <summary>
    /// 话-数量
    /// </summary>

    [SugarColumn(ColumnDescription = "话-数量")]
    public int ChapterCount { get; set; }

    /// <summary>
    /// 页-数量
    /// </summary>

    [SugarColumn(ColumnDescription = "页-数量")]
    public int PageCount { get; set; }

    /// <summary>
    /// 处理模式 (Zip / Folder / Network)
    /// </summary>

    [SugarColumn(ColumnDataType = "varchar(32)", ColumnDescription = "处理模式")]
    public string ProcessMode { get; set; } = "Folder";

    /// <summary>
    /// 存储路径
    /// </summary>

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "存储路径")]
    public string? StoragePath { get; set; }

    /// <summary>
    /// 扩展Id（用于存放额外id，比如来自网络的id）
    /// </summary>

    [SugarColumn(IsNullable = true, ColumnDescription = "扩展Id")]
    public string? ExtendId { get; set; }

    /// <summary>
    /// 扩展路径（用于存放额外路径，比如来自网络的路径）
    /// </summary>

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "扩展路径")]
    public string? ExtendPath { get; set; }

    /// <summary>
    /// 备注
    /// </summary>

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "备注")]
    public string? Remark { get; set; }

    

    /// <summary>
    /// 作者
    /// </summary>
    [Navigate(typeof(LocalComicAuthorMapping), nameof(LocalComicAuthorMapping.ComicId),
        nameof(LocalComicAuthorMapping.AuthorId))]
    public List<LocalAuthor>? Authors { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    [Navigate(typeof(LocalComicTagMapping), nameof(LocalComicTagMapping.ComicId),
        nameof(LocalComicTagMapping.TagId))]
    public List<Sdk.Models.ShadowTag>? Tags { get; set; }
}