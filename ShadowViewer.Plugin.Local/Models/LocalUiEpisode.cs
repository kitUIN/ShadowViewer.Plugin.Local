using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 
/// </summary>
public partial class LocalUiEpisode : ObservableObject, IUiEpisode
{
    /// <summary>
    /// 
    /// </summary>
    public ComicChapter Source { get; set; }

    [ObservableProperty] public partial string Title { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="episode"></param>
    public LocalUiEpisode(ComicChapter episode)
    {
        Source = episode;
        Title = episode.Name;
    }
}