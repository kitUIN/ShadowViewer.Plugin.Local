using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Models;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

public partial class LocalUiEpisode : ObservableObject, IUiEpisode
{
    public LocalEpisode Source { get; set; }

    [ObservableProperty] private string title;

    public LocalUiEpisode(LocalEpisode episode)
    {
        Source = episode;
        Title = episode.Name;
    }
}