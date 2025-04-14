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
using ShadowViewer.Plugin.Local.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using ShadowViewer.Plugin.Local.Enums;

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
    /// 点击菜单检测
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ScrollViewer_Tapped(object sender, TappedRoutedEventArgs e)
    {
        Menu.Visibility = (Menu.Visibility != Visibility.Visible).ToVisibility();
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
}