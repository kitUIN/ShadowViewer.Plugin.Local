using DryIoc;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ShadowPluginLoader.WinUI;
using ShadowPluginLoader.WinUI.Helpers;
using ShadowViewer.Sdk.Args;
using ShadowViewer.Sdk.Responders;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Serilog;
using Serilog.Core;
using ShadowViewer.Plugin.Local.Configs;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 
/// </summary>
public sealed partial class PicPage : Page
{
    /// <summary>
    /// ViewModel
    /// </summary>
    public PicViewModel ViewModel { get; } = DiFactory.Services.Resolve<PicViewModel>();
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
        autoPageTimer.Tick += ((_, _) =>
        {
            if (LocalPluginConfig.PageAutoTurn && !ViewModel.IsMenu) MangaReader?.NextPage();
        });
        SettingsHelper.SettingChanged += SettingsHelper_SettingChanged;
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
            {
                Container: "ShadowViewer.Plugin.Local",
                Key: nameof(LocalPluginConfig.PageAutoTurnInterval)
            })
        {
            autoPageTimer.Stop();
            autoPageTimer.Interval = TimeSpan.FromSeconds((double)e.Value);
            autoPageTimer.Start();
        }
    }

    /// <summary>
    /// 控制翻页的计时器
    /// </summary>
    private readonly DispatcherQueueTimer autoPageTimer;


    /// <summary>
    /// 导航进入
    /// </summary>
    /// <param name="e"></param>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not PicViewArg arg) return;
        ViewModel.Affiliation = arg.Affiliation;
        ViewModel.Init(arg);
        autoPageTimer.Start();
        Task.Run(() =>
        {
            DispatcherQueue.TryEnqueue(async void () =>
            {
                try
                {
                    while (true)
                    {
                        await MangaReader.StartSmoothScrollAsync(LocalPluginConfig.PageAutoTurnInterval * 1000);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("SmoothScroll Error:{e}", ex);
                }
            });
        });
    }

    /// <summary>
    /// 滚轮翻页响应
    /// </summary>
    private void MangaPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // var thread = CoreWindow.GetForCurrentThread();
        // if (thread != null)
        // {
        //     var ctrlState = thread.GetKeyState(VirtualKey.Control);
        //     if (ctrlState.HasFlag(CoreVirtualKeyStates.Down))
        //         return;
        // }
        if ((int)LocalPluginConfig.LocalReaderMode > 1 || ViewModel.IsMenu) return;
        var point = e.GetCurrentPoint(this);
        var delta = point.Properties.MouseWheelDelta;
        var scrollSteps = delta / 120;
        if (scrollSteps == 0) return;
        if (scrollSteps > 0)
        {
            for (var i = 0; i < scrollSteps; i++)
                MangaReader.PrevPage();
        }
        else
        {
            for (var i = 0; i < -scrollSteps; i++)
                MangaReader.NextPage();
        }
    }

    private void PageTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;
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
        MangaReader.NextPage();
    }

    private void PrevPageTapped()
    {
        if (ViewModel.TappedGridSetting || ViewModel.IsMenu) return;
        MangaReader.PrevPage();
    }


    private void TappedGridSet(object sender, RoutedEventArgs e)
    {
        ViewModel.ScrollingPaddingEnabled =
            MangaReader.ReadMode == LocalReaderMode.VerticalScrolling && !ViewModel.TappedGridSetting;
        if (ViewModel.TappedGridSetting) return;
        LocalPluginConfig.TappedGridLayout = new ApplicationDataCompositeValue
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

        if (layout.TryGetValue("Row0", out var row0) && layout.TryGetValue("Row0_Unit", out var row0Unit))
            TappedGrid.RowDefinitions[0].Height = new GridLength((double)row0, (GridUnitType)(int)row0Unit);

        if (layout.TryGetValue("Row2", out var row2) && layout.TryGetValue("Row2_Unit", out var row2Unit))
            TappedGrid.RowDefinitions[2].Height = new GridLength((double)row2, (GridUnitType)(int)row2Unit);

        if (layout.TryGetValue("Row4", out var row4) && layout.TryGetValue("Row4_Unit", out var row4Unit))
            TappedGrid.RowDefinitions[4].Height = new GridLength((double)row4, (GridUnitType)(int)row4Unit);

        if (layout.TryGetValue("Col0", out var col0) && layout.TryGetValue("Col0_Unit", out var col0Unit))
            TappedGrid.ColumnDefinitions[0].Width = new GridLength((double)col0, (GridUnitType)(int)col0Unit);

        if (layout.TryGetValue("Col2", out var col2) && layout.TryGetValue("Col2_Unit", out var col2Unit))
            TappedGrid.ColumnDefinitions[2].Width = new GridLength((double)col2, (GridUnitType)(int)col2Unit);

        if (layout.TryGetValue("Col4", out var col4) && layout.TryGetValue("Col4_Unit", out var col4Unit))
            TappedGrid.ColumnDefinitions[4].Width = new GridLength((double)col4, (GridUnitType)(int)col4Unit);
    }

    private void ReadModeClosed(object? sender, object e)
    {
        ViewModel.ScrollingPaddingEnabled =
            MangaReader.ReadMode == LocalReaderMode.VerticalScrolling && !ViewModel.TappedGridSetting;
    }
}