using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Core.Models;
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
    public LocalEpisode Source { get; set; }

    [ObservableProperty] private string title;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="episode"></param>
    public LocalUiEpisode(LocalEpisode episode)
    {
        Source = episode;
        Title = episode.Name;
    }
}