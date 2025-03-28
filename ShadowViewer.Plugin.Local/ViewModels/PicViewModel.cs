using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Controls.Attributes;
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
    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private ILogger Logger { get; }

    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private ISqlSugarClient Db { get; }

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

    #region 阅读模式

    /// <summary>
    /// 阅读模式,滚动:<see cref="LocalReadMode.ScrollingReadMode"/>>;双页翻页:<see cref="LocalReadMode.TwoPageReadMode"/>
    /// </summary>
    [ObservableProperty] private LocalReadMode readMode = LocalReadMode.TwoPageReadMode;
    /// <summary>
    /// 阅读模式图标
    /// </summary>
    [ObservableProperty] private Icon readModeIcon;
    /// <summary>
    /// 阅读模式图标类型
    /// </summary>
    [ObservableProperty] private IconVariant readModeIconVariant = IconVariant.Regular;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>

    partial void OnReadModeChanged(LocalReadMode oldValue, LocalReadMode newValue)
    {
        if (oldValue == newValue) return;
        var field = typeof(LocalReadMode).GetField(ReadMode.ToString()!);
        var icon = field?.GetCustomAttribute<MenuFlyoutItemIconAttribute>();
        if(icon == null) return;
        ReadModeIcon = icon.Icon;
        ReadModeIconVariant = icon.IconVariant;
    }

    #endregion

    public string Affiliation { get; set; }

    private IPicViewResponder? PicViewResponder { get; set; }

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