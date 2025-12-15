namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 漫画与作者中间表
/// </summary>
public class LocalComicAuthorMapping
{
    /// <summary>
    /// 漫画Id
    /// </summary>
    public long ComicId { get; set; }
        
    /// <summary>
    /// 作者Id
    /// </summary>
    public long AuthorId { get; set; }
}