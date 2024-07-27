using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using ShadowViewer.Args;
using ShadowViewer.Interfaces;
using ShadowViewer.Models;
using ShadowViewer.Responders;
using ShadowViewer.Services;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.ViewModels;

public partial class PicViewModel : ObservableObject
{
    private ILogger Logger { get; }
    public ObservableCollection<IShadowPicture> Images { get; set; } = new();
    public LocalComic Comic { get; private set; }
    [ObservableProperty] private int currentEpisodeIndex = -1;
    public ObservableCollection<IShadowEpisode> Episodes { get; } = new();
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private bool isMenu;
    public string Affiliation { get; set; }
    private ISqlSugarClient Db { get; }
    private ResponderService ResponderService { get; }

    public PicViewModel(ILogger logger, ISqlSugarClient sqlSugarClient,
        ResponderService responderService)
    {
        Logger = logger;
        Db = sqlSugarClient;
        ResponderService = responderService;
    }

    public void Init(PicViewArg arg)
    {
        Affiliation = arg.Affiliation;
        Images.Clear();
        Episodes.Clear();
        ResponderService.GetEnabledResponder<IPicViewResponder>(Affiliation)
            ?.PicturesLoadStarting(this, arg);
    }

    partial void OnCurrentEpisodeIndexChanged(int oldValue, int newValue)
    {
        ResponderService.GetEnabledResponder<IPicViewResponder>(Affiliation)
            ?.CurrentEpisodeIndexChanged(this, Affiliation, oldValue, newValue);
    }
}