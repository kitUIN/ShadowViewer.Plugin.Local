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
    /// 进度条是否被点击(拖动)
    /// </summary>
    private bool isPageSliderPressed;

    /// <summary>
    /// 
    /// </summary>
    public PicPage()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// 滚动响应
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void PicViewer_Loaded(object sender, RoutedEventArgs e)
    {
        var hostScrollViewer = PicViewer.FindDescendant<ScrollViewer>();
        if (hostScrollViewer == null)
        {
            throw new InvalidOperationException(
                "This behavior can only be attached to an element which has a ScrollViewer as a parent.");
        }

        hostScrollViewer.ViewChanged -= ParentScrollViewer_ViewChanged;
        hostScrollViewer.ViewChanged += ParentScrollViewer_ViewChanged;
        hostScrollViewer.Tapped -= ScrollViewer_Tapped;
        hostScrollViewer.Tapped += ScrollViewer_Tapped;
    }

    /// <summary>
    /// 移动视图响应
    /// </summary>
    private void ParentScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (isPageSliderPressed) return;
        var hostScrollViewer = (ScrollViewer)sender!;
        for (var i = ViewModel.Images.Count - 1; i > 0; i--)
        {
            var associatedObject = PicViewer.ContainerFromIndex(i) as FrameworkElement;
            if (associatedObject == null) continue;
            var associatedElementRect = associatedObject
                .TransformToVisual(hostScrollViewer)
                .TransformBounds(new Rect(0, 0, associatedObject.ActualWidth, associatedObject.ActualHeight));

            var hostScrollViewerRect = new Rect(0, 0, hostScrollViewer.ActualWidth, hostScrollViewer.ActualHeight);

            if (!hostScrollViewerRect.Contains(new Point(associatedElementRect.Left, associatedElementRect.Top)) &&
                !hostScrollViewerRect.Contains(new Point(associatedElementRect.Right, associatedElementRect.Top)) &&
                !hostScrollViewerRect.Contains(new Point(associatedElementRect.Right,
                    associatedElementRect.Bottom)) &&
                !hostScrollViewerRect.Contains(new Point(associatedElementRect.Left, associatedElementRect.Bottom)))
                continue;
            ViewModel.CurrentPage = i + 1;
            break;
        }
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
            isPageSliderPressed = true;
            await PicViewer.SmoothScrollIntoViewWithIndexAsync(ViewModel.CurrentPage - 1, ScrollItemPlacement.Top,true);
            isPageSliderPressed = false;
        };
        ViewModel.Init(arg);
    }
    /// <summary>
    /// 按键检测
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ScrollViewer_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.PageDown && sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ChangeView(null, scrollViewer.VerticalOffset + scrollViewer.ViewportHeight, null);
        }
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
                !isPageSliderPressed) return;
            await PicViewer.SmoothScrollIntoViewWithIndexAsync((int)(e.NewValue - 1), ScrollItemPlacement.Top, disableAnimation: true);
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
            if(ViewModel.CurrentPage - 1 >= 0 && ViewModel.CurrentPage - 1 < ViewModel.Images.Count)
                await PicViewer.SmoothScrollIntoViewWithIndexAsync(ViewModel.CurrentPage - 1, ScrollItemPlacement.Top, disableAnimation: true);
            isPageSliderPressed = false;
        }
        catch (Exception ex)
        {
            Log.Error("监听是否松开点击进度条报错: {e}", ex);
        }
    }

    /// <summary>
    /// 监听是否点击进度条
    /// </summary>
    private void PageSlider_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        isPageSliderPressed = true;
    }

    /// <summary>
    /// 进度条加载完毕事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PageSlider_OnLoaded(object sender, RoutedEventArgs e)
    {
        var slider = (Slider)sender;
        slider.AddHandler(PointerPressedEvent, new PointerEventHandler(PageSlider_OnPointerPressed), true);
        slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(PageSlider_OnPointerReleased), true);
    }
}