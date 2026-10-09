using ShadowViewer.Plugin.Local.Models;
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

    public long ComicId { get; set; }

    /// <summary>
    /// 话-数量
    /// </summary>

    public int ChapterCount { get; set; }

    /// <summary>
    /// 页-数量
    /// </summary>

    public int PageCount { get; set; }

    /// <summary>
    /// 处理模式 (Zip / Folder / Network)
    /// </summary>

    public string ProcessMode { get; set; } = "Folder";

    /// <summary>
    /// 存储路径
    /// </summary>

    public string? StoragePath { get; set; }

    /// <summary>
    /// 扩展Id（用于存放额外id，比如来自网络的id）
    /// </summary>

    public string? ExtendId { get; set; }

    /// <summary>
    /// 扩展路径（用于存放额外路径，比如来自网络的路径）
    /// </summary>

    public string? ExtendPath { get; set; }

    /// <summary>
    /// 备注
    /// </summary>

    public string? Remark { get; set; }

    

    /// <summary>
    /// 作者
    /// </summary>
    public List<LocalAuthor>? Authors { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public List<Sdk.Models.ShadowTag>? Tags { get; set; }
}
