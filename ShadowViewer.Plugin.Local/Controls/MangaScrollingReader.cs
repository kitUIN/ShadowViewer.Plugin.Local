using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ShadowViewer.Plugin.Local.Enums;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;

namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 滑动阅读器的封装
/// </summary>
public class MangaScrollingReader : ListView
{
    /// <summary>
    /// 
    /// </summary>
    public MangaScrollingReader() : base()
    {
        SelectionMode = ListViewSelectionMode.None;
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
    /// 
    /// </summary>
    private ScrollViewer? hostScrollViewer;

    /// <summary>
    /// 移动视图响应
    /// </summary>
    private void ParentScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (IgnoreViewChanged) return;
        var scrollViewer = (ScrollViewer)sender!;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var associatedObject = ContainerFromIndex(i) as FrameworkElement;
            if (associatedObject == null) continue;
            var associatedElementRect = associatedObject
                .TransformToVisual(scrollViewer)
                .TransformBounds(new Rect(0, 0, associatedObject.ActualWidth, associatedObject.ActualHeight));

            var hostScrollViewerRect = new Rect(0, 0, scrollViewer.ActualWidth, scrollViewer.ActualHeight);

            if (!hostScrollViewerRect.Contains(new Point(associatedElementRect.Left, associatedElementRect.Top)) &&
                !hostScrollViewerRect.Contains(new Point(associatedElementRect.Right, associatedElementRect.Top)) &&
                !hostScrollViewerRect.Contains(new Point(associatedElementRect.Right,
                    associatedElementRect.Bottom)) &&
                !hostScrollViewerRect.Contains(new Point(associatedElementRect.Left, associatedElementRect.Bottom)))
                continue;
            if (i + 1 != CurrentIndex) CurrentIndex = i + 1;
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
        if (sender is not ScrollViewer scrollViewer) return;
        if (e.Key == VirtualKey.PageDown)
        {
            scrollViewer.ChangeView(null, scrollViewer.VerticalOffset + scrollViewer.ViewportHeight, null);
        }
        else if (e.Key == VirtualKey.PageUp)
        {
            scrollViewer.ChangeView(null, scrollViewer.VerticalOffset - scrollViewer.ViewportHeight, null);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public LocalReaderMode ReadMode
    {
        get => (LocalReaderMode)GetValue(ReadModeProperty);
        set => SetValue(ReadModeProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty ReadModeProperty =
        DependencyProperty.Register(nameof(ReadMode), typeof(LocalReaderMode), typeof(MangaReader),
            new PropertyMetadata(LocalReaderMode.ScrollingReadMode, OnReadModeChanged));

    /// <summary>
    /// 
    /// </summary>
    private static void OnReadModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MangaScrollingReader)d;
        var mode = (LocalReaderMode)e.NewValue;
        control.Visibility = mode == LocalReaderMode.ScrollingReadMode ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        hostScrollViewer = GetTemplateChild("ScrollViewer") as ScrollViewer;
        if(hostScrollViewer == null) return;
        hostScrollViewer.ViewChanged -= ParentScrollViewer_ViewChanged;
        hostScrollViewer.ViewChanged += ParentScrollViewer_ViewChanged;
    }
}