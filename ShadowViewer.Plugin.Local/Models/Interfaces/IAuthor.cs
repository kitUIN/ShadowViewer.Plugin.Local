namespace ShadowViewer.Plugin.Local.Models.Interfaces;

/// <summary>
/// 作者基类
/// </summary>
public interface IAuthor
{
    /// <summary>
    /// 雪花Id
    /// </summary>
    long Id { get; set; }
    /// <summary>
    /// 作者名称
    /// </summary>
    string Name { get; set; }
}
