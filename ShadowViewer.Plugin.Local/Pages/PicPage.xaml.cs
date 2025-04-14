using CommunityToolkit.WinUI;
using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core.Args;
using ShadowViewer.Core.Extensions;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

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

    /// <summary>
    /// 
    /// </summary>
    public PicPage()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// 导航进入
    /// </summary>
    /// <param name="e"></param>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not PicViewArg arg) return;
        ViewModel.Affiliation = arg.Affiliation;
        ViewModel.LastPicturePositionLoadedEvent += async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(0.3));
            ViewModel.IsPageSliderPressed = true;
            await PicViewer.SmoothScrollIntoViewWithIndexAsync(ViewModel.CurrentPage - 1, ScrollItemPlacement.Top,true);
            ViewModel.IsPageSliderPressed = false;
        };
        ViewModel.Init(arg);
    }


    /// <summary>
    /// 移动进度条跳转
    /// </summary>
    private async void PageSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            if (!(e.NewValue - 1 >= 0) || !(e.NewValue - 1 < ViewModel.Images.Count) ||
                !ViewModel.IsPageSliderPressed) return;
            ViewModel.CurrentPage = (int)e.NewValue;
            await PicViewer.SmoothScrollIntoViewWithIndexAsync((int)(e.NewValue - 1),
                ScrollItemPlacement.Top, disableAnimation: true);
        }
        catch (Exception ex)
        {
            Log.Error("移动进度条响应报错: {e}", ex);
        }
    }

    /// <summary>
    /// 监听是否松开点击进度条
    /// </summary>
    private async void PageSlider_OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (ViewModel.CurrentPage - 1 >= 0 &&
                ViewModel.CurrentPage - 1 < ViewModel.Images.Count)
                await PicViewer.SmoothScrollIntoViewWithIndexAsync(ViewModel.CurrentPage - 1,
                    ScrollItemPlacement.Top, disableAnimation: true);
        }
        catch (Exception ex)
        {
            Log.Error("监听是否松开点击进度条报错: {e}", ex);
        }
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
        if (LocalPlugin.Settings.LocalReaderMode != LocalReaderMode.TwoPageReadMode) return;
        var point = e.GetCurrentPoint(this);
        var delta = point.Properties.MouseWheelDelta;
        var scrollSteps = delta / 120;
        if (scrollSteps == 0) return;
        if (scrollSteps > 0)
        {
            for (var i = 0; i < scrollSteps; i++)
                ViewModel.CurrentPage = int.Max(1, ViewModel.CurrentPage - 2);
        }
        else
        {
            for (var i = 0; i < -scrollSteps; i++)
                ViewModel.CurrentPage = int.Min(ViewModel.Images.Count, ViewModel.CurrentPage + 2);
        }
    }

    private void MenuTapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.IsMenu = !ViewModel.IsMenu;
    }

    private void NextPageTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.TappedGridSetting) return;
        if (!ViewModel.NextPageCommand.CanExecute(null)) return;
        ViewModel.NextPageCommand.Execute(null);
    }
    private void PrevPageTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.TappedGridSetting) return;
        if (!ViewModel.PrevPageCommand.CanExecute(null)) return;
        ViewModel.PrevPageCommand.Execute(null);
    }


    private void TappedGridSet(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TappedGridSetting) return;
        LocalPlugin.Settings.TappedGridLayout = new ApplicationDataCompositeValue
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
        var layout = LocalPlugin.Settings.TappedGridLayout;

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

}