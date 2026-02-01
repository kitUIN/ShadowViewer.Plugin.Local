using System.Collections.Generic;
using Windows.Storage;
using ShadowViewer.Plugin.Local.Entities;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 导入预览
/// </summary>
public class ComicImportPreview
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 是否需要密码
    /// </summary>
    public bool IsPasswordRequired { get; set; }

    /// <summary>
    /// 缩略图
    /// </summary>
    public string Thumb { get; set; } = "mx-appx:///default.png";

    /// <summary>
    /// 漫画详情
    /// </summary>
    public ComicDetail ComicDetail { get; set; } = new();

    /// <summary>
    /// 预览章节
    /// </summary>
    public List<ComicChapter> PreviewChapters { get; set; } = [];

    /// <summary>
    /// 子漫画（用于多漫画文件夹导入）
    /// </summary>
    public List<ComicImportPreview> SubPreviews { get; set; } = [];

    /// <summary>
    /// 是否有子漫画
    /// </summary>
    public bool HasSubPreviews => SubPreviews?.Count > 0;

    /// <summary>
    /// 是否有章节
    /// </summary>
    public bool HasChapters => PreviewChapters?.Count > 0;

    /// <summary>
    /// 源文件
    /// </summary>
    public IStorageItem SourceItem { get; set; } = null!;

    /// <summary>
    /// 预计算的图片列表（按章节ID分组，或临时存储）
    /// 用于优化导入，避免再次扫描文件夹
    /// Key: Chapter Name? No, we don't have ids yet.
    /// Use simple list mapping? 
    /// </summary>
    public Dictionary<ComicChapter, List<ComicPicture>> PreviewImages { get; set; } = new();
}
