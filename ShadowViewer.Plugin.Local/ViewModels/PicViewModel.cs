using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using ShadowViewer.Core.Args;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Responders;
using ShadowViewer.Core.Services;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.ViewModels;

public partial class PicViewModel : ObservableObject
{
    private ILogger Logger { get; }
    /// <summary>
    /// 图片
    /// </summary>
    public ObservableCollection<IUiPicture> Images { get; set; } = [];
    public LocalComic Comic { get; private set; }
    [ObservableProperty] private int currentEpisodeIndex = -1;
    public ObservableCollection<IUiEpisode> Episodes { get; } = [];
    public List<int> EpisodeCounts { get; } = [];
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private bool isMenu;
    /// <summary>
    /// 进度条是否被点击(拖动)
    /// </summary>
    [ObservableProperty] private bool isPageSliderPressed;

    /// <summary>
    /// 阅读模式,滚动:<see cref="LocalReadMode.Scrolling"/>>;双页翻页:<see cref="LocalReadMode.TwoPage"/>
    /// </summary>
    [ObservableProperty] private LocalReadMode readMode = LocalReadMode.TwoPage;
    public string Affiliation { get; set; }
    private ISqlSugarClient Db { get; }
    private IPicViewResponder? PicViewResponder { get; set; }
    public PicViewModel(ILogger logger, ISqlSugarClient sqlSugarClient)
    {
        Logger = logger;
        Db = sqlSugarClient;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="arg"></param>
    public void Init(PicViewArg arg)
    {
        Affiliation = arg.Affiliation;
        if (arg.Parameter is LocalComic comic) Comic = comic;
        Images.Clear();
        Episodes.Clear();
        EpisodeCounts.Clear();
        PicViewResponder = ResponderHelper.GetEnabledResponder<IPicViewResponder>(Affiliation);
        PicViewResponder?.PicturesLoadStarting(this, arg);
    }

    partial void OnCurrentEpisodeIndexChanged(int oldValue, int newValue)
    {
        PicViewResponder?.CurrentEpisodeIndexChanged(this, Affiliation, oldValue, newValue);
    }

    partial void OnCurrentPageChanged(int oldValue, int newValue)
    {
        PicViewResponder?.CurrentPageIndexChanged(this, Affiliation, oldValue, newValue);
    }

    /// <summary>
    /// 加载上次阅读事件
    /// </summary>
    public event EventHandler LastPicturePositionLoadedEvent;

    /// <summary>
    /// 加载上次阅读
    /// </summary>
    public void LastPicturePositionLoaded()
    {
        LastPicturePositionLoadedEvent?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine("LastPicturePositionLoaded");
    }
}