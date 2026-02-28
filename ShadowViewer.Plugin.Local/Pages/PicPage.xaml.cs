using DryIoc;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using ShadowPluginLoader.WinUI;
using ShadowPluginLoader.WinUI.Helpers;
using ShadowViewer.Plugin.Local.Configs;
using ShadowViewer.Plugin.Local.Constants;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowViewer.Sdk.Args;
using System;
using System.Collections.Generic;
using ShadowViewer.Plugin.Local.Readers;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 
/// </summary>
public sealed partial class PicPage
{
    /// <summary>
    /// ViewModel
    /// </summary>
    public PicViewModel ViewModel { get; } = DiFactory.Services.Resolve<PicViewModel>();

    /// <summary>
    /// 
    /// </summary>
    public LocalPluginConfig LocalPluginConfig { get; } = DiFactory.Services.Resolve<LocalPluginConfig>();


    /// <summary>
    /// 
    /// </summary>
    public PicPage()
    {
        this.InitializeComponent();
        autoPageTimer = DispatcherQueue.CreateTimer();
        autoPageTimer.IsRepeating = true;
        autoPageTimer.Interval = TimeSpan.FromSeconds(LocalPluginConfig.PageAutoTurnInterval);
    }

    /// <summary>
    /// 自动翻页计时器回调。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">事件参数。</param>
    private void AutoPageTimer_Tick(DispatcherQueueTimer sender, object e)
    {
        if (LocalPluginConfig.PageAutoTurn && !ViewModel.IsMenu)
        {
            ViewModel.NextPageCommand.Execute(null);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="System.NotImplementedException"></exception>
    private void SettingsHelper_SettingChanged(object? sender, ShadowPluginLoader.WinUI.Args.SettingChangedArgs e)
    {
        if (e is
            not
            {
                Container: PluginConstants.PluginId,
                Key: nameof(LocalPluginConfig.PageAutoTurnInterval)
            }) return;
        autoPageTimer.Stop();
        autoPageTimer.Interval = TimeSpan.FromSeconds((double)e.Value);
        autoPageTimer.Start();
    }

    /// <summary>
    /// 控制翻页的计时器
    /// </summary>
    private readonly DispatcherQueueTimer autoPageTimer;

    /// <summary>
    /// 标记页面清理流程是否已执行，避免重复解绑导致的冗余操作。
    /// </summary>
    private bool isPageCleanupCompleted;


    /// <summary>
    /// 导航进入
    /// </summary>
    /// <param name="e"></param>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not PicViewArg arg) return;

        // 页面可能被重用，先去重再订阅，确保事件链只保留一份。
        autoPageTimer.Tick -= AutoPageTimer_Tick;
        autoPageTimer.Tick += AutoPageTimer_Tick;
        SettingsHelper.SettingChanged -= SettingsHelper_SettingChanged;
        SettingsHelper.SettingChanged += SettingsHelper_SettingChanged;
        Unloaded -= PicPage_Unloaded;
        Unloaded += PicPage_Unloaded;

        isPageCleanupCompleted = false;
        ViewModel.Affiliation = arg.Affiliation;
        ViewModel.Init(arg);
        autoPageTimer.Start();
    }

    /// <summary>
    /// 导航离开时释放页面资源，避免页面实例被静态事件或计时器持续持有。
    /// </summary>
    /// <param name="e">导航事件参数。</param>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        CleanupPageResources();
        base.OnNavigatedFrom(e);
    }

    /// <summary>
    /// 页面卸载时兜底清理，覆盖非导航触发的可视树移除场景。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">路由事件参数。</param>
    private void PicPage_Unloaded(object sender, RoutedEventArgs e)
    {
        CleanupPageResources();
    }

    /// <summary>
    /// 释放页面与阅读器相关资源。
    /// </summary>
    private void CleanupPageResources()
    {
        if (isPageCleanupCompleted)
        {
            return;
        }

        isPageCleanupCompleted = true;

        autoPageTimer.Stop();
        autoPageTimer.Tick -= AutoPageTimer_Tick;
        SettingsHelper.SettingChanged -= SettingsHelper_SettingChanged;
        Unloaded -= PicPage_Unloaded;

        MangaReader.ClearItems(scheduleLayoutUpdate: false);
        ViewModel.ReleaseResources();
    }

    private void PageTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;
        if (ViewModel is { TappedGridSetting: true, IsMenu: true })
        {
            return;
        }

        if (ViewModel.IsMenu)
        {
            MenuTapped();
            return;
        }

        var position = e.GetPosition(grid);
        var colStar = 0D;
        var col0 = TappedGrid.ColumnDefinitions[0].Width;
        var col2 = TappedGrid.ColumnDefinitions[2].Width;
        var col4 = TappedGrid.ColumnDefinitions[4].Width;
        if (col0.GridUnitType == GridUnitType.Star) colStar += col0.Value;
        if (col2.GridUnitType == GridUnitType.Star) colStar += col2.Value;
        if (col4.GridUnitType == GridUnitType.Star) colStar += col4.Value;
        var avWidth = grid.ActualWidth / colStar;

        var x1 = col0.GridUnitType == GridUnitType.Star ? col0.Value * avWidth : col0.Value;
        var x2 = x1 + (col2.GridUnitType == GridUnitType.Star ? col2.Value * avWidth : col2.Value);

        var rowStar = 0D;
        var row0 = TappedGrid.RowDefinitions[0].Height;
        var row2 = TappedGrid.RowDefinitions[2].Height;
        var row4 = TappedGrid.RowDefinitions[4].Height;
        if (row0.GridUnitType == GridUnitType.Star) rowStar += row0.Value;
        if (row2.GridUnitType == GridUnitType.Star) rowStar += row2.Value;
        if (row4.GridUnitType == GridUnitType.Star) rowStar += row4.Value;
        var avHeight = grid.ActualHeight / rowStar;

        var y1 = row0.GridUnitType == GridUnitType.Star ? row0.Value * avHeight : row0.Value;
        var y2 = y1 + (row2.GridUnitType == GridUnitType.Star ? row2.Value * avHeight : row2.Value);

        if (position.X < x1 || position.X < x2 && position.Y < y1)
        {
            PrevPageTapped();
        }
        else if (position.X > x2 || position.X > x1 && position.Y > y2)
        {
            NextPageTapped();
        }
        else
        {
            MenuTapped();
        }
    }

    private void MenuTapped()
    {
        ViewModel.IsMenu = !ViewModel.IsMenu;
    }

    private void NextPageTapped()
    {
        if (ViewModel.TappedGridSetting || ViewModel.IsMenu) return;
        ViewModel.NextPageCommand.Execute(null);
    }

    private void PrevPageTapped()
    {
        if (ViewModel.TappedGridSetting || ViewModel.IsMenu) return;
        ViewModel.PrevPageCommand.Execute(null);
    }


    private void TappedGridSet(object sender, RoutedEventArgs e)
    {
        ViewModel.ScrollingPaddingEnabled =
            MangaReader.Mode == ReadingMode.VerticalScroll && !ViewModel.TappedGridSetting;
        if (ViewModel.TappedGridSetting) return;
        LocalPluginConfig.TappedGridLayout = new Dictionary<string, double>()
        {
            ["Row0"] = TappedGrid.RowDefinitions[0].Height.Value,
            ["Row0_Unit"] = (int)TappedGrid.RowDefinitions[0].Height.GridUnitType,
            ["Row2"] = TappedGrid.RowDefinitions[2].Height.Value,
            ["Row2_Unit"] = (int)TappedGrid.RowDefinitions[2].Height.GridUnitType,
            ["Row4"] = TappedGrid.RowDefinitions[4].Height.Value,
            ["Row4_Unit"] = (int)TappedGrid.RowDefinitions[4].Height.GridUnitType,

            ["Col0"] = TappedGrid.ColumnDefinitions[0].Width.Value,
            ["Col0_Unit"] = (int)TappedGrid.ColumnDefinitions[0].Width.GridUnitType,
            ["Col2"] = TappedGrid.ColumnDefinitions[2].Width.Value,
            ["Col2_Unit"] = (int)TappedGrid.ColumnDefinitions[2].Width.GridUnitType,
            ["Col4"] = TappedGrid.ColumnDefinitions[4].Width.Value,
            ["Col4_Unit"] = (int)TappedGrid.ColumnDefinitions[4].Width.GridUnitType,
        };
    }


    private void InitTappedGridLayout(object sender, RoutedEventArgs e)
    {
        var layout = LocalPluginConfig.TappedGridLayout;
        if (layout is null) return;

        if (layout.TryGetValue("Row0", out var row0) &&
            layout.TryGetValue("Row0_Unit", out var row0Unit))
            TappedGrid.RowDefinitions[0].Height = new GridLength(row0, (GridUnitType)(int)row0Unit);

        if (layout.TryGetValue("Row2", out var row2) &&
            layout.TryGetValue("Row2_Unit", out var row2Unit))
            TappedGrid.RowDefinitions[2].Height = new GridLength(row2, (GridUnitType)(int)row2Unit);

        if (layout.TryGetValue("Row4", out var row4) &&
            layout.TryGetValue("Row4_Unit", out var row4Unit))
            TappedGrid.RowDefinitions[4].Height = new GridLength(row4, (GridUnitType)(int)row4Unit);

        if (layout.TryGetValue("Col0", out var col0) &&
            layout.TryGetValue("Col0_Unit", out var col0Unit))
            TappedGrid.ColumnDefinitions[0].Width = new GridLength(col0, (GridUnitType)(int)col0Unit);

        if (layout.TryGetValue("Col2", out var col2) &&
            layout.TryGetValue("Col2_Unit", out var col2Unit))
            TappedGrid.ColumnDefinitions[2].Width = new GridLength(col2, (GridUnitType)(int)col2Unit);

        if (layout.TryGetValue("Col4", out var col4) &&
            layout.TryGetValue("Col4_Unit", out var col4Unit))
            TappedGrid.ColumnDefinitions[4].Width = new GridLength(col4, (GridUnitType)(int)col4Unit);
    }

    private void ReadModeClosed(object? sender, object e)
    {
        ViewModel.ScrollingPaddingEnabled =
            MangaReader.Mode == ReadingMode.VerticalScroll && !ViewModel.TappedGridSetting;
    }

    private void IgnoreTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }
}