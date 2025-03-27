using System;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Serilog;
using ShadowViewer.Core.Args;

namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 阅读页的进度条封装
/// </summary>
public class ReaderSlider: Slider
{
    /// <summary>
    /// 
    /// </summary>
    public ReaderSlider() : base()
    {
        Loaded += PageSlider_OnLoaded;
    }

    /// <summary>
    /// 在进度条上按下
    /// </summary>
    public bool SliderPressed
    {
        get => (bool)GetValue(SliderPressedProperty);
        set => SetValue(SliderPressedProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty SliderPressedProperty =
        DependencyProperty.Register(nameof(SliderPressed), typeof(bool), typeof(ReaderSlider),
            new PropertyMetadata(false));


    /// <summary>
    /// 监听是否松开点击进度条
    /// </summary>
    private void PageSlider_OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        SliderPressed = false;
    }

    /// <summary>
    /// 监听是否点击进度条
    /// </summary>
    private void PageSlider_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        SliderPressed = true;
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