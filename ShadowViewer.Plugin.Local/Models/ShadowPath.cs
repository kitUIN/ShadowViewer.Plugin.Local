using System.Collections.Generic;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 路径树
/// </summary>
public class ShadowPath
{
    private readonly LocalComic comic;

    /// <summary>
    /// 
    /// </summary>
    public string Name => comic.Name;

    /// <summary>
    /// 
    /// </summary>
    public long Id => comic.Id;

    /// <summary>
    /// 
    /// </summary>
    public string Thumb => comic.Thumb;

    /// <summary>
    /// 
    /// </summary>
    public bool IsFolder => comic.IsFolder;

    /// <summary>
    /// 
    /// </summary>
    public List<ShadowPath> Children { get; } = [];

    /// <summary>
    /// 
    /// </summary>
    /// <param name="comic"></param>
    public ShadowPath(LocalComic comic)
    {
        this.comic = comic;
    }
}