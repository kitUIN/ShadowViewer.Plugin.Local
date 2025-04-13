using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ShadowViewer.Plugin.Local.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;
using DispatcherQueuePriority = Microsoft.UI.Dispatching.DispatcherQueuePriority;

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
        Loaded += (_, _) => ScrollViewerInit(true);
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
    /// 滚动响应
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void ScrollViewerInit(bool init=false)
    {
        if (hostScrollViewer != null) return;
        if (ReadMode != LocalReadMode.ScrollingReadMode) return;
        Task.Run(() =>
        {
            if (!init) Thread.Sleep(TimeSpan.FromSeconds(0.5));
            this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                var scrollViewer = this.FindDescendant<ScrollViewer>();
                if (scrollViewer == null) return;
                hostScrollViewer = scrollViewer;
                hostScrollViewer.ViewChanged -= ParentScrollViewer_ViewChanged;
                hostScrollViewer.ViewChanged += ParentScrollViewer_ViewChanged;
            });
        });
    }
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
            if(i + 1 != CurrentIndex) CurrentIndex = i + 1;
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
            new PropertyMetadata(LocalReadMode.ScrollingReadMode, OnReadModeChanged));

    /// <summary>
    /// 
    /// </summary>
    private static void OnReadModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MangaScrollingReader)d;
        var mode = (LocalReadMode)e.NewValue;
        control.Visibility = mode == LocalReadMode.ScrollingReadMode ? Visibility.Visible : Visibility.Collapsed;
        if (mode == LocalReadMode.ScrollingReadMode) control.ScrollViewerInit();
    }
}