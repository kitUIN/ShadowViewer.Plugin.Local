using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShadowViewer.Sdk.Args;
using ShadowViewer.Sdk.Helpers;
using ShadowViewer.Sdk.Responders;

namespace ShadowViewer.Plugin.Local.ViewModels;

public partial class PicViewModel : ObservableObject
{
    /// <summary>
    /// 
    /// </summary>
    [Autowired]
    private ILogger Logger { get; }


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
    [NotifyPropertyChangedFor(nameof(NextEpisodeTip))]
    [ObservableProperty]
    public partial int CurrentEpisodeIndex { get; set; } = -1;

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextEpisodeTip))]
    public partial int CurrentPageIndex { get; set; }

    /// <summary>
    ///  下一页显示
    /// </summary> 
    public bool NextEpisodeTip => CurrentPageIndex + 1 == Images.Count && CanNextEpisode;

    /// <summary>
    /// 菜单可见性
    /// </summary>
    [ObservableProperty]
    public partial bool IsMenu { get; set; }


    /// <summary>
    /// 点击区域设定模式
    /// </summary>
    [NotifyPropertyChangedFor(nameof(MenuOpacity))]
    [ObservableProperty]
    public partial bool TappedGridSetting { get; set; }

    /// <summary>
    /// 滚动填充设置
    /// </summary>
    [ObservableProperty]
    public partial bool ScrollingPaddingSetting { get; set; }

    /// <summary>
    /// 滚动填充启用状态
    /// </summary>
    [ObservableProperty]
    public partial bool ScrollingPaddingEnabled { get; set; }

    /// <summary>
    /// 菜单透明度
    /// </summary>
    public double MenuOpacity => TappedGridSetting ? 0.7 : 1;

    /// <summary>
    /// 类别(插件id)
    /// </summary>
    public string Affiliation { get; set; }

    /// <summary>
    /// 
    /// </summary>
    private IPicViewResponder? PicViewResponder { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public PicViewContext Context { get; private set; }

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
        Context = new PicViewContext(Affiliation, arg.Parameter, new Dictionary<string, object>());
        PicViewResponder = ResponderHelper.GetEnabledResponder<IPicViewResponder>(Affiliation);
        if (PicViewResponder is null)
        {
            Logger.Error("IPicViewResponder[{T}] not existed", Affiliation);
        }

        PicViewResponder?.PicturesLoadStarting(this, Context);
    }

    /// <summary>
    /// <inheritdoc cref="IPicViewResponder.CurrentEpisodeIndexChanged"/>
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnCurrentEpisodeIndexChanged(int oldValue, int newValue)
    {
        PicViewResponder?.CurrentEpisodeIndexChanged(this, Context, oldValue, newValue);
    }

    /// <summary>
    /// <inheritdoc cref="IPicViewResponder.CurrentPageIndexChanged"/>
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnCurrentPageIndexChanged(int oldValue, int newValue)
    {
        PicViewResponder?.CurrentPageIndexChanged(this, Context, oldValue, newValue);
    }

    /// <summary>
    /// 加载上次阅读事件
    /// </summary>
    public event EventHandler? LastPicturePositionLoadedEvent;

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

    /// <summary>
    /// 上一话
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPrevEpisode))]
    private void PrevEpisode()
    {
        CurrentEpisodeIndex -= 1;
    }

    /// <summary>
    /// 允许下一页
    /// </summary>
    public bool CanNextPage => Images.Count > CurrentPageIndex + 1;

    /// <summary>
    /// 允许上一页
    /// </summary>
    public bool CanPrevPage => Images.Count > CurrentPageIndex - 1 && CurrentPageIndex > 0;

    /// <summary>
    /// 下一页
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private void NextPage()
    {
        if (!CanNextPage) return;
        CurrentPageIndex += 1;
    }

    /// <summary>
    /// 上一页
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPrevPage))]
    private void PrevPage()
    {
        if (!CanPrevPage) return;
        CurrentPageIndex -= 1;
    }

    /// <summary>
    /// Currents the page.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns></returns>
    public int CurrentPage(int index)
    {
        return index + 1;
    }
    /// <summary>
    /// Currents the page back.
    /// </summary>
    /// <param name="currentPage">The current page.</param>
    public void CurrentPageBack(double currentPage)
    {
        CurrentPageIndex = Convert.ToInt32(currentPage) - 1;
    }
}