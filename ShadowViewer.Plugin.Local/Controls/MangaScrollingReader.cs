using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ShadowViewer.Core.Args;
using ShadowViewer.Plugin.Local.Enums;
using System;
using Windows.Foundation;
using Windows.System;

namespace ShadowViewer.Plugin.Local.Controls;


/// <summary>
/// 滑动阅读器的封装
/// </summary>
public class MangaScrollingReader: ListView
{
    /// <summary>
    /// 
    /// </summary>
    public MangaScrollingReader():base()
    {
        SelectionMode = ListViewSelectionMode.None;
        Loaded += PicViewer_Loaded;
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IgnoreViewChanged
    {
        get => (bool)GetValue(IgnoreViewChangedProperty);
        set => SetValue(IgnoreViewChangedProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty IgnoreViewChangedProperty =
        DependencyProperty.Register(nameof(IgnoreViewChanged), typeof(bool), typeof(MangaScrollingReader),
            new PropertyMetadata(false));
    /// <summary>
    /// 
    /// </summary>
    public int CurrentIndex
    {
        get => (int)GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CurrentIndexProperty =
        DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(MangaScrollingReader),
            new PropertyMetadata(0, OnCurrentIndexChanged));
    /// <summary>
    /// 
    /// </summary>
    private static void OnCurrentIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { 
    }
    /// <summary>
    /// 滚动响应
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void PicViewer_Loaded(object sender, RoutedEventArgs e)
    {
        var hostScrollViewer = this.FindDescendant<ScrollViewer>();
        if (hostScrollViewer == null) return;
        hostScrollViewer.ViewChanged -= ParentScrollViewer_ViewChanged;
        hostScrollViewer.ViewChanged += ParentScrollViewer_ViewChanged;
    }
    /// <summary>
    /// 移动视图响应
    /// </summary>
    private void ParentScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (IgnoreViewChanged) return;
        var hostScrollViewer = (ScrollViewer)sender!;
        for (var i = Items.Count - 1; i > 0; i--)
        {
            var associatedObject = ContainerFromIndex(i) as FrameworkElement;
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
            CurrentIndex = i + 1;
            break;
        }
    }

    /// <summary>
    /// 上下按键检测
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
    /// 
    /// </summary>
    public LocalReadMode ReadMode
    {
        get => (LocalReadMode)GetValue(ReadModeProperty);
        set => SetValue(ReadModeProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty ReadModeProperty =
        DependencyProperty.Register(nameof(ReadMode), typeof(LocalReadMode), typeof(MangaReader),
            new PropertyMetadata(LocalReadMode.Scrolling, OnReadModeChanged));

    /// <summary>
    /// 
    /// </summary>
    private static void OnReadModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MangaScrollingReader)d;
        var mode = (LocalReadMode)e.NewValue;
        control.Visibility = mode == LocalReadMode.Scrolling ? Visibility.Visible : Visibility.Collapsed;
    }
}