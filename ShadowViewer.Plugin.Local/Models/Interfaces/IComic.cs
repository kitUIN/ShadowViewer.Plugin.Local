using System;
using System.Collections.Generic;

namespace ShadowViewer.Plugin.Local.Models.Interfaces;

/// <summary>
/// 漫画基类
/// </summary>
public interface IComic
{
    /// <summary>
    /// 雪花Id
    /// </summary>
    long Id { get; set; }
    /// <summary>
    /// 父级
    /// </summary>
    long ParentId { get; set; }
    /// <summary>
    /// 漫画Id
    /// </summary>
    string ComicId { get; set; }
    /// <summary>
    /// 漫画名称
    /// </summary>
    string Name { get; set; }
    /// <summary>
    /// 缩略图
    /// </summary>
    string Thumb { get; set; }
    /// <summary>
    /// 作者
    /// </summary>
    List<IAuthor> Authors { get; set; }
    /// <summary>
    /// 创建日期
    /// </summary>
    DateTime CreatedDateTime { get; set; }
    /// <summary>
    /// 更新日期
    /// </summary>
    DateTime UpdatedDateTime { get; set; }
    /// <summary>
    /// 话-数量
    /// </summary>
    int EpisodeCount { get; set; }
    /// <summary>
    /// 页-数量
    /// </summary>
    int Count { get; set; }
    /// <summary>
    /// 是否是文件夹
    /// </summary>
    bool IsFolder { get; set; }
    /// <summary>
    /// 分类(插件Id)
    /// </summary>
    string Affiliation { get; set; }
    /// <summary>
    /// 存储大小
    /// </summary>
    long Size { get; set; }
    /// <summary>
    /// 备注
    /// </summary>
    string Remark { get; set; }
    /// <summary>
    /// 路径链接
    /// </summary>
    string Link { get; set; }
}