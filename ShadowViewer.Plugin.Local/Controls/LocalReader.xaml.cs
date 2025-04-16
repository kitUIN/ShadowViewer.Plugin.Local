using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Foundation;


namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 本地阅读器
/// </summary>
public sealed partial class LocalReader : UserControl
{
    /// <summary>
    /// 
    /// </summary>
    public LocalReader()
    {
        this.InitializeComponent();
        Loaded += (sender, args) =>
        {
            CheckScrollViewerEvent();
            ReadMode = ReadMode;
        };
    }

    /// <summary>
    /// 
    /// </summary>
    private List<IReadingModeStrategy> ReadingModeStrategies { get; } =
    [
        new SinglePageStrategy(),
        new DoublePageStrategy(),
        new VerticalScrollingStrategy(),
        new VerticalScrollingStrategy(),
    ];

    /// <summary>
    /// 
    /// </summary>
    public IReadingModeStrategy ReadingModeStrategy => ReadingModeStrategies[(int)ReadMode];

    /// <summary>
    /// 
    /// </summary>
    public Thickness ScrollPadding
    {
        get => (Thickness)GetValue(ScrollPaddingProperty);
        set => SetValue(ScrollPaddingProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty ScrollPaddingProperty =
        DependencyProperty.Register(nameof(ScrollPadding), typeof(Thickness), typeof(LocalReader),
            new PropertyMetadata(new Thickness(0)));


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
        DependencyProperty.Register(nameof(ReadMode), typeof(LocalReaderMode), typeof(LocalReader),
            new PropertyMetadata(LocalReaderMode.DoublePage, OnReadModeChanged));

    /// <summary>
    /// 
    /// </summary>
    private static void OnReadModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;
        VisualStateManager.GoToState(control, control.ReadMode.ToString(), true);
        control.CheckScrollViewerEvent();
        control.ReadingModeStrategy.OnCurrentIndexChanged(control, control.CurrentIndex, control.CurrentIndex);
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
        DependencyProperty.Register(nameof(IgnoreViewChanged), typeof(bool), typeof(LocalReader),
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
        DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(LocalReader),
            new PropertyMetadata(0, OnCurrentIndexChanged));

    /// <summary>
    /// 
    /// </summary>
    private static void OnCurrentIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;
        control.ReadingModeStrategy.OnCurrentIndexChanged(control, (int)e.OldValue, (int)e.NewValue);
    }

    /// <summary>
    /// 
    /// </summary>
    public void CheckCanPage()
    {
        CanNextPage = ReadingModeStrategy.CanNextPage(this);
        CanPrevPage = ReadingModeStrategy.CanPrevPage(this);
    }

    /// <summary>
    /// 
    /// </summary>
    public ImageSource? CurrentLeftPage
    {
        get => (ImageSource?)GetValue(CurrentLeftPageProperty);
        set => SetValue(CurrentLeftPageProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CurrentLeftPageProperty =
        DependencyProperty.Register(nameof(CurrentLeftPage), typeof(ImageSource), typeof(LocalReader),
            new PropertyMetadata(null));

    /// <summary>
    /// 
    /// </summary>
    public ImageSource? CurrentRightPage
    {
        get => (ImageSource?)GetValue(CurrentRightPageProperty);
        set => SetValue(CurrentRightPageProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CurrentRightPageProperty =
        DependencyProperty.Register(nameof(CurrentRightPage), typeof(ImageSource), typeof(LocalReader),
            new PropertyMetadata(null));


    /// <summary>
    /// 
    /// </summary>
    public IList<IUiPicture> Pictures
    {
        get => (IList<IUiPicture>)GetValue(PicturesProperty);
        set => SetValue(PicturesProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty PicturesProperty =
        DependencyProperty.Register(nameof(Pictures), typeof(IList<IUiPicture>), typeof(LocalReader),
            new PropertyMetadata(new ObservableCollection<IUiPicture>(), null));

    /// <summary>
    /// 
    /// </summary>
    public bool CanNextPage
    {
        get => (bool)GetValue(CanNextPageProperty);
        private set => SetValue(CanNextPageProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CanNextPageProperty =
        DependencyProperty.Register(nameof(CanNextPage), typeof(bool), typeof(LocalReader),
            new PropertyMetadata(false));

    /// <summary>
    /// 
    /// </summary>
    public bool CanPrevPage
    {
        get => (bool)GetValue(CanPrevPageProperty);
        private set => SetValue(CanPrevPageProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CanPrevPageProperty =
        DependencyProperty.Register(nameof(CanPrevPage), typeof(bool), typeof(LocalReader),
            new PropertyMetadata(false));

    /// <summary>
    /// 
    /// </summary>
    private ScrollViewer? hostScrollViewer;

    /// <summary>
    /// 检查事件是否添加
    /// </summary>
    public void CheckScrollViewerEvent()
    {
        if (hostScrollViewer != null) return;
        hostScrollViewer = PicViewer.FindDescendant<ScrollViewer>();
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
        var scrollViewer = (ScrollViewer)sender!;
        for (var i = Pictures.Count - 1; i >= 0; i--)
        {
            var associatedObject = PicViewer.ContainerFromIndex(i) as FrameworkElement;
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
    /// 下一页
    /// </summary>
    public void NextPage() => ReadingModeStrategy.NextPage(this);

    /// <summary>
    /// 上一页
    /// </summary>
    public void PrevPage() => ReadingModeStrategy.PrevPage(this);

    /// <summary>
    /// 滑动到页
    /// </summary>
    public void ScrollIntoCurrentPage(int toPage)
    {
        if ((int)LocalPlugin.Settings.LocalReaderMode <= 1 || !IgnoreViewChanged) return;
        if (PicViewer.Items == null || PicViewer.Items.Count < toPage) return;
        PicViewer.ScrollIntoView(PicViewer.Items[toPage - 1], ScrollIntoViewAlignment.Default);
    }

    /// <summary>
    /// 滑动到当前页面长度
    /// </summary>
    public void ScrollIntoOffset(bool next)
    {
        if ((int)LocalPlugin.Settings.LocalReaderMode <= 1 || hostScrollViewer == null) return;
        var offset = next
            ? hostScrollViewer.VerticalOffset + this.ActualHeight
            : hostScrollViewer.VerticalOffset - this.ActualHeight;
        if (next && offset > hostScrollViewer.ScrollableHeight) offset = hostScrollViewer.ScrollableHeight;
        else if (!next && offset < 0) offset = 0;
        hostScrollViewer.ScrollToVerticalOffset(offset);
    }
}