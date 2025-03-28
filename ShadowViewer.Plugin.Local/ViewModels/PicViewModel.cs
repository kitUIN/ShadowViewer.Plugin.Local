using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Controls.Attributes;
using ShadowViewer.Core.Args;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Responders;
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

    /// <summary>
    /// 当前漫画
    /// </summary>
    public LocalComic Comic { get; private set; }

    /// <summary>
    /// 当前话
    /// </summary>
    [NotifyCanExecuteChangedFor(nameof(NextEpisodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrevEpisodeCommand))]
    [ObservableProperty]
    private int currentEpisodeIndex = -1;

    /// <summary>
    /// 话
    /// </summary>
    public ObservableCollection<IUiEpisode> Episodes { get; } = [];

    /// <summary>
    /// 话索引
    /// </summary>
    public List<int> EpisodeCounts { get; } = [];

    /// <summary>
    /// 当前页
    /// </summary>
    [ObservableProperty] private int currentPage = 1;

    /// <summary>
    /// 菜单可见性
    /// </summary>
    [ObservableProperty] private bool isMenu;

    /// <summary>
    /// 进度条是否被点击(拖动)
    /// </summary>
    [ObservableProperty] private bool isPageSliderPressed;


    #region 阅读模式

    /// <summary>
    /// 阅读模式,滚动:<see cref="LocalReadMode.ScrollingReadMode"/>>;双页翻页:<see cref="LocalReadMode.TwoPageReadMode"/>
    /// </summary>
    [ObservableProperty] private LocalReadMode readMode;

    /// <summary>
    /// 阅读模式图标
    /// </summary>
    [ObservableProperty] private Icon readModeIcon;

    /// <summary>
    /// 阅读模式图标类型
    /// </summary>
    [ObservableProperty] private IconVariant readModeIconVariant = IconVariant.Regular;


    /// <summary>
    /// Constructor Init
    /// </summary>
    partial void ConstructorInit()
    {
        OnReadModeChanged(ReadMode);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    partial void OnReadModeChanged(LocalReadMode value)
    {
        var field = typeof(LocalReadMode).GetField(value.ToString()!);
        var icon = field?.GetCustomAttribute<MenuFlyoutItemIconAttribute>();
        if (icon == null) return;
        ReadModeIcon = icon.Icon;
        ReadModeIconVariant = icon.IconVariant;
    }

    #endregion

    /// <summary>
    /// 类别(插件id)
    /// </summary>
    public string Affiliation { get; set; }

    /// <summary>
    /// 
    /// </summary>
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnCurrentEpisodeIndexChanged(int oldValue, int newValue)
    {
        PicViewResponder?.CurrentEpisodeIndexChanged(this, Affiliation, oldValue, newValue);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
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
    }

    /// <summary>
    /// 允许下一话
    /// </summary>
    public bool CanNextEpisode => Episodes.Count > CurrentEpisodeIndex + 1;

    /// <summary>
    /// 允许上一话
    /// </summary>
    public bool CanPrevEpisode => Episodes.Count > CurrentEpisodeIndex - 1 && CurrentEpisodeIndex > 0;

    /// <summary>
    /// 下一话
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanNextEpisode))]
    private void NextEpisode()
    {
        CurrentEpisodeIndex += 1;
    }

    [RelayCommand(CanExecute = nameof(CanPrevEpisode))]
    private void PrevEpisode()
    {
        CurrentEpisodeIndex -= 1;
    }
}