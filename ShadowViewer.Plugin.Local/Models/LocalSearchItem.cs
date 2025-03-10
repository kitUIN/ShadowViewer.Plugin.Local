using Microsoft.UI.Xaml.Controls;
using ShadowViewer.Core.Models.Interfaces;
using ShadowViewer.Plugin.Local.Enums;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 
/// </summary>
public class LocalSearchItem : IShadowSearchItem
{
    /// <inheritdoc />
    public string Title { get; set; }

    /// <inheritdoc />
    public string SubTitle { get; set; }

    /// <inheritdoc />
    public string Id { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string ComicId { get; set; }

    /// <inheritdoc />
    public IconSource Icon { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public LocalSearchMode Mode { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="id"></param>
    /// <param name="comicId"></param>
    /// <param name="mode"></param>
    public LocalSearchItem(string title, string id,string comicId, LocalSearchMode mode)
    {
        Title = title;
        Mode = mode;
        switch (mode)
        {
            case LocalSearchMode.SearchComic:
                SubTitle = "本地漫画";
                Icon = new FontIconSource() { Glyph = "\uE82D" };
                break;
            case LocalSearchMode.SearchTag:
                SubTitle = "本地标签";
                break;
            default:
                break;
        }

        ComicId = comicId;
        Id = id;
    }
}