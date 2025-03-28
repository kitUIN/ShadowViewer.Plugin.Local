using Windows.System;
using CommunityToolkit.WinUI;
using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Core.Args;
using ShadowViewer.Core.Extensions;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Serilog;

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
}