using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ShadowViewer.Plugin.Local.Configs;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using ShadowPluginLoader.WinUI;
using DryIoc;
using System.Diagnostics;

namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 一个集成了阅读器功能的可缩放、可拖动虚拟图像控件
/// 整合了 LocalReader 的所有功能，包括单页、双页、纵向滚动等阅读模式
/// </summary>
public sealed class LocalReader : Control
{
    private static readonly HttpClient HttpClient = new();
    private LocalPluginConfig LocalPluginConfig { get; } = DiFactory.Services.Resolve<LocalPluginConfig>();

    // Canvas 相关
    private CanvasVirtualControl canvas;
    private Dictionary<string, CanvasBitmap> bitmapCache = new();
    private List<CanvasBitmap> bitmaps = new();
    private readonly List<Rect> pageRects = new();
    private Size contentSize = new(0, 0);
    private int loadVersion = 0;
    private int lastReportedPageIndex = -1;

    // 拖拽和缩放相关
    private Vector2 offset = Vector2.Zero;
    private Vector2 velocity = Vector2.Zero;
    private bool dragging = false;
    private Point lastPoint;

    // 阅读策略
    private List<IReadingModeStrategy> ReadingModeStrategies { get; } =
    [
        new SinglePageStrategy(),
        new DoublePageStrategy(),
        new VerticalScrollingStrategy(),
    ];

    // 垂直滚动窗口优化
    private const int ImageWindowSize = 3;
    private List<string> allImagePaths = new();
    private Dictionary<string, Size> _pageSizeCache = new();
    private bool _isUpdatingIndexFromScroll = false;
    private List<string> _lastLoadedSources = new();
    private bool _isLayoutDirty = false;

    private IReadingModeStrategy ReadingModeStrategy => ReadingModeStrategies[(int)ReaderMode];

    public LocalReader()
    {
        this.DefaultStyleKey = typeof(LocalReader);
        
        Loaded += async (_, _) =>
        {
            ReaderMode = ReaderMode;
        };
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        canvas = GetTemplateChild("PART_Canvas") as CanvasVirtualControl;

        if (canvas != null)
        {
            canvas.ClearColor = Colors.Black;
            canvas.RegionsInvalidated += Canvas_RegionsInvalidated;
            canvas.PointerPressed += Canvas_PointerPressed;
            canvas.PointerMoved += Canvas_PointerMoved;
            canvas.PointerReleased += Canvas_PointerReleased;
            canvas.PointerWheelChanged += Canvas_PointerWheelChanged;
            canvas.DoubleTapped += Canvas_DoubleTapped;
            canvas.ManipulationMode = ManipulationModes.Scale;
            canvas.ManipulationDelta += Canvas_ManipulationDelta;
            canvas.SizeChanged += Canvas_SizeChanged;

            _ = ReloadImagesAsync();
        }
    }

    #region Dependency Properties - 图像源相关

    public IList<string> Sources
    {
        get => (IList<string>)GetValue(SourcesProperty);
        set => SetValue(SourcesProperty, value);
    }

    public static readonly DependencyProperty SourcesProperty =
        DependencyProperty.Register(nameof(Sources), typeof(IList<string>),
            typeof(LocalReader),
            new PropertyMetadata(null, OnSourcesChanged));

    #endregion

    #region Dependency Properties - 阅读器核心功能

    public LocalReaderMode ReaderMode
    {
        get => (LocalReaderMode)GetValue(ReaderModeProperty);
        set => SetValue(ReaderModeProperty, value);
    }

    public static readonly DependencyProperty ReaderModeProperty =
        DependencyProperty.Register(nameof(ReaderMode), typeof(LocalReaderMode),
            typeof(LocalReader),
            new PropertyMetadata(LocalReaderMode.DoublePage, OnDisplayModeChanged));

    public int CurrentIndex
    {
        get => (int)GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    public static readonly DependencyProperty CurrentIndexProperty =
        DependencyProperty.Register(nameof(CurrentIndex), typeof(int),
            typeof(LocalReader),
            new PropertyMetadata(0, OnCurrentIndexChanged));

    public int CurrentEpisodeIndex
    {
        get => (int)GetValue(CurrentEpisodeIndexProperty);
        set => SetValue(CurrentEpisodeIndexProperty, value);
    }

    public static readonly DependencyProperty CurrentEpisodeIndexProperty =
        DependencyProperty.Register(nameof(CurrentEpisodeIndex), typeof(int),
            typeof(LocalReader),
            new PropertyMetadata(-1, OnCurrentEpisodeIndexChanged));

    public IList<IUiPicture> Pictures
    {
        get => (IList<IUiPicture>)GetValue(PicturesProperty);
        set => SetValue(PicturesProperty, value);
    }

    public static readonly DependencyProperty PicturesProperty =
        DependencyProperty.Register(nameof(Pictures), typeof(IList<IUiPicture>),
            typeof(LocalReader),
            new PropertyMetadata(new ObservableCollection<IUiPicture>(), OnPicturesChanged));
    

    public bool CanNextPage
    {
        get => (bool)GetValue(CanNextPageProperty);
        private set => SetValue(CanNextPageProperty, value);
    }

    public static readonly DependencyProperty CanNextPageProperty =
        DependencyProperty.Register(nameof(CanNextPage), typeof(bool),
            typeof(LocalReader),
            new PropertyMetadata(false));

    public bool CanPrevPage
    {
        get => (bool)GetValue(CanPrevPageProperty);
        private set => SetValue(CanPrevPageProperty, value);
    }

    public static readonly DependencyProperty CanPrevPageProperty =
        DependencyProperty.Register(nameof(CanPrevPage), typeof(bool),
            typeof(LocalReader),
            new PropertyMetadata(false));

    public bool IgnoreViewChanged
    {
        get => (bool)GetValue(IgnoreViewChangedProperty);
        set => SetValue(IgnoreViewChangedProperty, value);
    }

    public static readonly DependencyProperty IgnoreViewChangedProperty =
        DependencyProperty.Register(nameof(IgnoreViewChanged), typeof(bool),
            typeof(LocalReader),
            new PropertyMetadata(false));

    public Thickness ScrollPadding
    {
        get => (Thickness)GetValue(ScrollPaddingProperty);
        set => SetValue(ScrollPaddingProperty, value);
    }

    public static readonly DependencyProperty ScrollPaddingProperty =
        DependencyProperty.Register(nameof(ScrollPadding), typeof(Thickness),
            typeof(LocalReader),
            new PropertyMetadata(new Thickness(0)));

    public bool SmoothScroll
    {
        get => (bool)GetValue(SmoothScrollProperty);
        set => SetValue(SmoothScrollProperty, value);
    }

    public static readonly DependencyProperty SmoothScrollProperty =
        DependencyProperty.Register(nameof(SmoothScroll), typeof(bool),
            typeof(LocalReader),
            new PropertyMetadata(false));

    public ObservableCollection<string> ImageSources
    {
        get => (ObservableCollection<string>)GetValue(ImageSourcesProperty);
        set => SetValue(ImageSourcesProperty, value);
    }

    public static readonly DependencyProperty ImageSourcesProperty =
        DependencyProperty.Register(nameof(ImageSources), typeof(ObservableCollection<string>),
            typeof(LocalReader),
            new PropertyMetadata(new ObservableCollection<string>()));

    #endregion

    #region Dependency Properties - 缩放和交互

    public float PageSpacing
    {
        get => (float)GetValue(PageSpacingProperty);
        set => SetValue(PageSpacingProperty, value);
    }

    public static readonly DependencyProperty PageSpacingProperty =
        DependencyProperty.Register(nameof(PageSpacing), typeof(float),
            typeof(LocalReader),
            new PropertyMetadata(16f, OnPageSpacingChanged));

    public Visibility IsLoading
    {
        get => (Visibility)GetValue(IsLoadingProperty);
        private set => SetValue(IsLoadingProperty, value);
    }

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(Visibility),
            typeof(LocalReader),
            new PropertyMetadata(Visibility.Visible));

    public float ZoomFactor
    {
        get => (float)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(nameof(ZoomFactor), typeof(float),
            typeof(LocalReader),
            new PropertyMetadata(1f, OnZoomFactorChanged));

    public bool EnableZoom
    {
        get => (bool)GetValue(EnableZoomProperty);
        set => SetValue(EnableZoomProperty, value);
    }

    public static readonly DependencyProperty EnableZoomProperty =
        DependencyProperty.Register(nameof(EnableZoom), typeof(bool),
            typeof(LocalReader),
            new PropertyMetadata(true));

    public bool EnableInertia
    {
        get => (bool)GetValue(EnableInertiaProperty);
        set => SetValue(EnableInertiaProperty, value);
    }

    public static readonly DependencyProperty EnableInertiaProperty =
        DependencyProperty.Register(nameof(EnableInertia), typeof(bool),
            typeof(LocalReader),
            new PropertyMetadata(true));

    public bool EnableDoubleTapZoom
    {
        get => (bool)GetValue(EnableDoubleTapZoomProperty);
        set => SetValue(EnableDoubleTapZoomProperty, value);
    }

    public static readonly DependencyProperty EnableDoubleTapZoomProperty =
        DependencyProperty.Register(nameof(EnableDoubleTapZoom), typeof(bool),
            typeof(LocalReader),
            new PropertyMetadata(true));

    #endregion

    #region Events

    public event EventHandler<int> VisiblePageChanged;

    #endregion

    #region Property Changed Handlers

    private static void OnSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;

        if (e.OldValue is INotifyCollectionChanged oldNotify)
            oldNotify.CollectionChanged -= control.OnSourcesCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newNotify)
            newNotify.CollectionChanged += control.OnSourcesCollectionChanged;

        control._isLayoutDirty = true;
        if (control.canvas == null) return;
        _ = control.ReloadImagesAsync();
    }

    private void OnSourcesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _isLayoutDirty = true;
        _ = ReloadImagesAsync();
    }

    private static void OnDisplayModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;
        control.ReadingModeStrategy.OnCurrentIndexChanged(control, control.CurrentIndex, control.CurrentIndex);

        if (control.ReaderMode == LocalReaderMode.VerticalScrolling)
        {
            _ = control.InitializeVerticalModeAsync();
        }
        else
        {
            try
            {
                control.VisiblePageChanged -= control.OnVisiblePageChangedInternal;
            }
            catch { }
        }

        if (control.canvas != null)
            _ = control.ReloadImagesAsync();
    }

    private static void OnCurrentIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;
        Debug.WriteLine($"[LocalReader] CurrentIndex changed from {(int)e.OldValue} to {(int)e.NewValue}");
        if (control.ReaderMode == LocalReaderMode.VerticalScrolling)
        {
            control.EnsureImageWindow((int)e.NewValue);

            if (!control._isUpdatingIndexFromScroll)
            {
                var prevIgnore = control.IgnoreViewChanged;
                control.IgnoreViewChanged = true;
                control.ScrollToPage((int)e.NewValue);
                Debug.WriteLine($"[LocalReader] ScrollToPage invoked for index {(int)e.NewValue}");
                control.IgnoreViewChanged = prevIgnore;
            }
        }
        else
        {
            control.ReadingModeStrategy.OnCurrentIndexChanged(control, (int)e.OldValue, (int)e.NewValue);
        }

        control.CheckCanPage();
    }

    private static void OnCurrentEpisodeIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;
        control.ReadingModeStrategy.OnCurrentIndexChanged(control, control.CurrentIndex, control.CurrentIndex);
    }

    private static void OnPicturesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;

        if (e.OldValue is INotifyCollectionChanged oldNotify)
            oldNotify.CollectionChanged -= control.Pictures_CollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newNotify)
            newNotify.CollectionChanged += control.Pictures_CollectionChanged;

        control.UpdateImageSourcesFromPictures();
    }

    private void Pictures_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateImageSourcesFromPictures();
    }

    private static void OnPageSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;
        control.UpdateLayoutMetrics();
        control.canvas?.Invalidate();
    }

    private static void OnZoomFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LocalReader)d;
        var newZoom = (float)e.NewValue;

        if (newZoom < 0.1f)
            control.ZoomFactor = 0.1f;
        else if (newZoom > 10f)
            control.ZoomFactor = 10f;

        control.canvas?.Invalidate();
    }

    #endregion

    #region 阅读器核心方法

    private async Task InitializeVerticalModeAsync()
    {
        await Task.Delay(50);

        var prevIgnore = IgnoreViewChanged;
        IgnoreViewChanged = true;

        UpdateImageSourcesFromPictures();
        EnsureImageWindow(CurrentIndex > 0 ? CurrentIndex : 1);

        if (Sources != null)
        {
            var newSources = new ObservableCollection<string>(Sources);
            ImageSources = newSources;
        }

        await Task.Delay(250);

        VisiblePageChanged -= OnVisiblePageChangedInternal;
        VisiblePageChanged += OnVisiblePageChangedInternal;

        IgnoreViewChanged = prevIgnore;
    }

    private void OnVisiblePageChangedInternal(object sender, int pageIndex)
    {
        if (IgnoreViewChanged) return;
        if (ReaderMode != LocalReaderMode.VerticalScrolling) return;

        _isUpdatingIndexFromScroll = true;
        try
        {
            CurrentIndex = pageIndex;
        }
        finally
        {
            _isUpdatingIndexFromScroll = false;
        }
    }

    private void UpdateImageSourcesFromPictures()
    {
        var list = new List<string>();
        if (Pictures != null)
        {
            foreach (var p in Pictures)
            {
                if (!string.IsNullOrWhiteSpace(p.SourcePath))
                    list.Add(p.SourcePath);
            }
        }

        allImagePaths = list;
        EnsureImageWindow(CurrentIndex > 0 ? CurrentIndex : 1);
    }

    private void EnsureImageWindow(int oneBasedIndex)
    {
        if (oneBasedIndex < 1) oneBasedIndex = 1;

        var start = oneBasedIndex - ImageWindowSize;
        var end = oneBasedIndex + ImageWindowSize;

        if (start < 1) start = 1;

        if (Pictures == null || Pictures.Count == 0 || allImagePaths.Count == 0)
        {
            Sources = new ObservableCollection<string>();
            return;
        }

        if (ImageSources != null && ImageSources.Count > 0 && 
            start - 1 < allImagePaths.Count && end - 1 < allImagePaths.Count &&
            ImageSources[0] == allImagePaths[start - 1] && 
            ImageSources[^1] == allImagePaths[Math.Min(end - 1, allImagePaths.Count - 1)])
            return;

        var newSources = new ObservableCollection<string>();
        for (var i = start - 1; i < end && i < allImagePaths.Count; i++)
        {
            newSources.Add(allImagePaths[i]);
        }

        Sources = newSources;
    }

    public void CheckCanPage()
    {
        CanNextPage = ReadingModeStrategy.CanNextPage(this);
        CanPrevPage = ReadingModeStrategy.CanPrevPage(this);
    }

    public void NextPage() => ReadingModeStrategy.NextPage(this);
    public void PrevPage() => ReadingModeStrategy.PrevPage(this);
    public void FirstPage() => CurrentIndex = 1;
    public void LastPage()
    {
        if (Pictures != null && Pictures.Count > 0)
            CurrentIndex = Pictures.Count;
    }

    public void OpenSettings()
    {
        try
        {
            var svc = DiFactory.Services.Resolve<object>("ShadowViewer.Plugin.Global.Controls.GlobalSettings");
            if (svc != null)
            {
                var showMethod = svc.GetType().GetMethod("Show");
                showMethod?.Invoke(svc, null);
            }
        }
        catch { }
    }

    #endregion

    #region 图像加载

    private async Task ReloadImagesAsync()
    {
        if (canvas == null) return;

        var currentVersion = ++loadVersion;
        if (bitmaps.Count == 0)
            IsLoading = Visibility.Visible;

        // 尝试捕获当前视图锚点（仅垂直滚动模式）
        string anchorPath = null;
        double anchorOldY = 0;
        bool hasAnchor = false;

        if (ReaderMode == LocalReaderMode.VerticalScrolling &&
            _lastLoadedSources != null &&
            pageRects.Count > 0 && // pageRects 现在可能是全量的
            canvas.ActualHeight > 0)
        {
            var viewportTop = -offset.Y / ZoomFactor;
            var viewportBottom = viewportTop + canvas.ActualHeight / ZoomFactor;

            // 在旧布局中找到第一个可视的图片作为锚点
            // 注意：此时 pageRects 尚未更新，仍是基于旧状态的（但如果之前已经是全量模式，Rect.Y应该是准的）
            // 如果是从非 Vertical 模式切换过来，或者是首次加载，pageRects 可能不准，但 hasAnchor 会处理
            
            for (int i = 0; i < pageRects.Count; i++)
            {
                var rect = pageRects[i];
                // 只要图片在视口范围内有重叠且高度大于0
                if (rect.Height > 0 && rect.Bottom > viewportTop && rect.Top < viewportBottom)
                {
                    // 找到对应的 Path
                    string path = null;
                    if (allImagePaths.Count == pageRects.Count) 
                    {
                        path = allImagePaths[i];
                    }
                    else if (_lastLoadedSources.Count == pageRects.Count) // 兼容旧逻辑/混合状态
                    {
                        path = _lastLoadedSources[i];
                    }

                    if (path != null)
                    {
                        anchorPath = path;
                        anchorOldY = rect.Y;
                        hasAnchor = true;
                        break;
                    }
                }
            }
        }

        try
        {
            var newBitmaps = new List<CanvasBitmap>();
            var currentSources = Sources;
            
            if (currentSources != null && currentSources.Count > 0)
            {
                // 使用HashSet加速查找
                var neededSources = new HashSet<string>(currentSources);

                foreach (var src in currentSources)
                {
                    if (currentVersion != loadVersion) return;

                    CanvasBitmap bitmap = null;
                    if (bitmapCache.TryGetValue(src, out var cached))
                    {
                        bitmap = cached;
                    }
                    else
                    {
                        bitmap = await LoadBitmapAsync(src);
                        if (bitmap != null && currentVersion == loadVersion)
                        {
                            bitmapCache[src] = bitmap;
                        }
                    }

                    if (bitmap != null && currentVersion == loadVersion)
                        newBitmaps.Add(bitmap);
                }

                // 清理不再使用的图片缓存
                if (currentVersion == loadVersion)
                {
                    var keysToRemove = bitmapCache.Keys.Where(k => !neededSources.Contains(k)).ToList();
                    foreach (var key in keysToRemove)
                    {
                        if (bitmapCache.TryGetValue(key, out var bmp))
                        {
                            bmp.Dispose();
                            bitmapCache.Remove(key);
                        }
                    }
                }
            }
            else
            {
                // 如果没有源，清理所有缓存
                foreach (var bmp in bitmapCache.Values) bmp.Dispose();
                bitmapCache.Clear();
            }

            if (currentVersion == loadVersion)
            {
                bitmaps = newBitmaps;
                UpdateLayoutMetrics();
                _isLayoutDirty = false;
                
                var totalLoadedCount = bitmaps.Count; // 保存局部变量供后续使用

                _lastLoadedSources = currentSources != null ? new List<string>(currentSources) : new();

                // 仅在非垂直滚动模式，或者初次加载时 ResetView
                if (ReaderMode != LocalReaderMode.VerticalScrolling)
                {
                    ResetView();
                }
                else
                {
                    bool positionRestored = false;

                    // 尝试恢复位置
                    if (hasAnchor && contentSize.Width > 0)
                    {
                        // 在新布局中找到锚点
                        // 此时 pageRects 应该已经包含所有路径
                        int newIndex = -1;
                        if (allImagePaths.Count > 0)
                        {
                            newIndex = allImagePaths.IndexOf(anchorPath);
                        }
                        
                        if (newIndex >= 0 && newIndex < pageRects.Count)
                        {
                            var newRect = pageRects[newIndex];
                            // 只有当 Y 坐标确实发生变化时才调整 offset
                            // 如果是向下滚动，前面的图片高度保留，diff 应该是 0
                            // 如果是向上滚动加载了前面的图片，diff < 0，offset 变小（向上推），符合预期
                            var diff = newRect.Y - anchorOldY;
                            if (Math.Abs(diff) > 0.001)
                            {
                                offset = new Vector2(offset.X, offset.Y - (float)(diff * ZoomFactor));
                                
                            }
                            positionRestored = true;
                        }
                    }

                    
                    bool resetViewCalled = false;

                    if (!positionRestored)
                    {
                        // 垂直滚动模式下，如果尚未初始化视图（ZoomFactor异常或offset为0且之前无内容），则重置
                        // 否则保持当前视图位置，并修正滚动位置到当前页
                        if (contentSize.Width > 0 && (ZoomFactor <= 0.01f || (offset == Vector2.Zero && totalLoadedCount <= ImageWindowSize)))
                        {
                            ResetView();
                            resetViewCalled = true;
                            ScrollToPage(CurrentIndex);
                        }
                        else
                        {
                           // 如果没有恢复位置（比如跳页了），尝试滚动到 CurrentIndex
                           // 但如果是滑动加载触发的重载，通常 hasAnchor 会命中，除非跳得太快
                           // 这里的 ScrollToPage 可能会打断滑动，所以仅当没有锚点时调用
                           if (!hasAnchor)
                               ScrollToPage(CurrentIndex);
                        }
                    }

                    // 如果没有重置视图（ResetView会处理居中），则手动更新水平偏移以保持居中
                    if (!resetViewCalled && contentSize.Width > 0 && canvas.ActualWidth > 0)
                    {
                        
                        offset.X = (float)((canvas.ActualWidth - contentSize.Width * ZoomFactor) / 2f);
                    }
                }

                canvas.Invalidate();
            }
        }
        finally
        {
            if (currentVersion == loadVersion)
                IsLoading = Visibility.Collapsed;
        }
    }

    private async Task<CanvasBitmap?> LoadBitmapAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        try
        {
            if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await HttpClient.GetByteArrayAsync(source);
                using var stream = new MemoryStream(bytes);
                return await CanvasBitmap.LoadAsync(canvas, stream.AsRandomAccessStream());
            }
            else
            {
                var file = await StorageFile.GetFileFromPathAsync(source);
                using var stream = await file.OpenReadAsync();
                return await CanvasBitmap.LoadAsync(canvas, stream);
            }
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Canvas 渲染

    private void Canvas_RegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args)
    {
        foreach (var region in args.InvalidatedRegions)
        {
            using var ds = sender.CreateDrawingSession(region);
            DrawContent(ds, region);
        }
    }

    private void DrawContent(CanvasDrawingSession ds, Rect region)
    {
        var canvasWidth = (float)canvas.ActualWidth;
        var canvasHeight = (float)canvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        ds.Clear(Colors.Black);

        // VerticalScrolling 模式下，pageRects 对应 allImagePaths
        if (ReaderMode == LocalReaderMode.VerticalScrolling)
        {
            if (pageRects.Count == 0) return;

            // 只需要遍历 pageRects，找到对应的图片进行绘制
            for (int i = 0; i < pageRects.Count; i++)
            {
                var rect = pageRects[i];
                if (rect.Height <= 0) continue; // 跳过占位符

                var scaledRect = new Rect(
                    rect.X * ZoomFactor + offset.X,
                    rect.Y * ZoomFactor + offset.Y,
                    rect.Width * ZoomFactor,
                    rect.Height * ZoomFactor
                );

                // 视口剔除
                if (scaledRect.Right < region.Left || scaledRect.Left > region.Right ||
                    scaledRect.Bottom < region.Top || scaledRect.Top > region.Bottom)
                    continue;

                // 获取图片
                CanvasBitmap bitmap = null;
                if (i < allImagePaths.Count)
                {
                    var path = allImagePaths[i];
                    bitmapCache.TryGetValue(path, out bitmap);
                }

                if (bitmap != null)
                {
                    ds.DrawImage(bitmap, scaledRect);
                }
            }
        }
        else
        {
            // 其他模式保持原有逻辑 (基于 bitmaps 列表)
            if (bitmaps.Count == 0) return;

            for (int i = 0; i < bitmaps.Count && i < pageRects.Count; i++)
            {
                var bitmap = bitmaps[i];
                var rect = pageRects[i];

                var scaledRect = new Rect(
                    rect.X * ZoomFactor + offset.X,
                    rect.Y * ZoomFactor + offset.Y,
                    rect.Width * ZoomFactor,
                    rect.Height * ZoomFactor
                );

                if (scaledRect.Right < region.Left || scaledRect.Left > region.Right ||
                    scaledRect.Bottom < region.Top || scaledRect.Top > region.Bottom)
                    continue;

                ds.DrawImage(bitmap, scaledRect);
            }
        }
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResetView();
        canvas?.Invalidate();
    }

    public void ResetView()
    {
        if (canvas == null || contentSize.Width <= 0 || contentSize.Height <= 0) return;

        var canvasWidth = (float)canvas.ActualWidth;
        var canvasHeight = (float)canvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var scaleX = canvasWidth / (float)contentSize.Width;
        var scaleY = canvasHeight / (float)contentSize.Height;

        if (ReaderMode == LocalReaderMode.VerticalScrolling)
        {
            ZoomFactor = scaleX;
            var scaledContentWidth = (float)contentSize.Width * ZoomFactor;
            offset = new Vector2(
                (canvasWidth - scaledContentWidth) / 2f,
                0
            );
        }
        else
        {
            ZoomFactor = Math.Min(scaleX, scaleY);
            var scaledContentWidth = (float)contentSize.Width * ZoomFactor;
            var scaledContentHeight = (float)contentSize.Height * ZoomFactor;

            offset = new Vector2(
                (canvasWidth - scaledContentWidth) / 2f,
                (canvasHeight - scaledContentHeight) / 2f
            );
        }
    }

    #endregion

    #region 交互处理

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        dragging = true;
        velocity = Vector2.Zero;
        lastPoint = e.GetCurrentPoint(canvas).Position;
        canvas.CapturePointer(e.Pointer);
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!dragging) return;

        var pos = e.GetCurrentPoint(canvas).Position;
        var delta = new Vector2((float)(pos.X - lastPoint.X), (float)(pos.Y - lastPoint.Y));

        offset += delta;
        velocity = delta;

        lastPoint = pos;
        canvas.Invalidate();
        NotifyViewportChanged();
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        dragging = false;
        canvas.ReleasePointerCapture(e.Pointer);

        if (EnableInertia)
            StartInertia();
    }

    private void StartInertia()
    {
        CompositionTarget.Rendering += OnInertiaFrame;
    }

    private void OnInertiaFrame(object sender, object e)
    {
        if (velocity.Length() < 0.1f)
        {
            CompositionTarget.Rendering -= OnInertiaFrame;
            return;
        }

        offset += velocity;
        velocity *= 0.90f;

        canvas.Invalidate();
        NotifyViewportChanged();
    }

    private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(canvas).Properties;
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
            Windows.System.VirtualKey.Control);

        if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            if (!EnableZoom) return;

            float zoom = props.MouseWheelDelta > 0 ? 1.1f : 0.9f;

            var pos = e.GetCurrentPoint(canvas).Position.ToVector2();
            offset = pos - (pos - offset) * zoom;

            
            NotifyViewportChanged();
        }
        else if (ReaderMode == LocalReaderMode.VerticalScrolling)
        {
            offset += new Vector2(0, props.MouseWheelDelta);
            
            offset += new Vector2(0, props.MouseWheelDelta);
            canvas.Invalidate();
            NotifyViewportChanged();
            e.Handled = true;
        }
    }

    private void Canvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (!EnableZoom) return;

        ZoomFactor *= (float)e.Delta.Scale;
        offset += e.Delta.Translation.ToVector2();

        canvas.Invalidate();
        NotifyViewportChanged();
    }

    private void Canvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!EnableDoubleTapZoom) return;

        if (ZoomFactor < 2f)
            ZoomFactor = 2f;
        else
            ResetView();

        canvas.Invalidate();
    }

    public void ScrollToPage(int pageIndex)
    {
        if (ReaderMode != LocalReaderMode.VerticalScrolling || canvas == null) return;
        
        // 垂直模式下 pageRects 对应 allImagePaths (通常情况)
        // 或者是 Sources (如果 allImagePaths 为空)
        // 优先使用 allImagePaths
        if (allImagePaths != null && allImagePaths.Count > 0)
        {
             if (pageIndex >= 1 && pageIndex <= pageRects.Count && pageIndex <= allImagePaths.Count)
             {
                 var rect = pageRects[pageIndex - 1];
                 // 只有当页面有高度时才滚动（或者是占位符？）
                 
                 // 即使高度为0，Y坐标也是准确的（等于下一页的起始位置）
                 offset = new Vector2(offset.X, -((float)rect.Y) * ZoomFactor);
                 canvas.Invalidate();
                 NotifyViewportChanged();
             }
        }
        else
        {
            // Fallback for empty allImagePaths (unlikely)
            int relativeIndex = -1;
            if (Sources != null && pageIndex >= 1 && pageIndex <= Sources.Count) // Assuming pageIndex matches Source index if allImagePaths is missing
            {
                relativeIndex = pageIndex - 1;
            }

            if (relativeIndex >= 0 && relativeIndex < pageRects.Count)
            {
                
                var rect = pageRects[relativeIndex];
                offset = new Vector2(offset.X, -((float)rect.Y) * ZoomFactor);
                canvas.Invalidate();
                NotifyViewportChanged();
            }
        }
    }

    public void ScrollByViewport(float deltaPixels)
    {
        
        if (ReaderMode != LocalReaderMode.VerticalScrolling || canvas == null) return;
        offset += new Vector2(0f, -deltaPixels);
        canvas.Invalidate();
        NotifyViewportChanged();
    }

    public int GetVisiblePageIndex()
    {
        if (_isLayoutDirty) return lastReportedPageIndex > 0 ? lastReportedPageIndex : CurrentIndex;
        if (ReaderMode != LocalReaderMode.VerticalScrolling || canvas == null) return 0;
        if (pageRects.Count == 0) return 0;
        if (ZoomFactor <= 0.0001f) return 0;

        var viewportCenterY = ((-offset.Y) + (float)canvas.ActualHeight / 2f) / ZoomFactor;
        
        // Vertical模式下，pageRects 对应 allImagePaths
        // 直接查找命中 rect 的索引即可
        int foundIndex = -1;

        for (int i = 0; i < pageRects.Count; i++)
        {
            var rect = pageRects[i];
            // 考虑高度为0的情况，应该跳过？或者如果正好在那个位置...
            if (rect.Height <= 0) continue; 
            
            if (viewportCenterY <= rect.Bottom)
            {
                foundIndex = i;
                break;
            }
        }
        
        if (foundIndex == -1 && pageRects.Count > 0)
            foundIndex = pageRects.Count - 1;

        if (foundIndex >= 0)
        {
             // 如果使用 allImagePaths，index + 1 即为页码
             if (allImagePaths != null && allImagePaths.Count > 0)
                return foundIndex + 1;
             else
                return foundIndex + 1; // Fallback
        }

        return 0;
    }

    private void NotifyViewportChanged()
    {
        if (_isLayoutDirty) return;
        if (ReaderMode != LocalReaderMode.VerticalScrolling) return;
        var pageIndex = GetVisiblePageIndex();
        if (pageIndex <= 0 || pageIndex == lastReportedPageIndex) return;
        lastReportedPageIndex = pageIndex;
        VisiblePageChanged?.Invoke(this, pageIndex);
    }

    #endregion

    #region 布局计算

    private void UpdateLayoutMetrics()
    {
        pageRects.Clear();
        contentSize = new Size(0, 0);

        if (bitmaps.Count == 0) return;

        float spacing = Math.Max(0f, PageSpacing);

        switch (ReaderMode)
        {
            case LocalReaderMode.DoublePage:
            {
                var leftBitmap = bitmaps.Count > 0 ? bitmaps[0] : null;
                var rightBitmap = bitmaps.Count > 1 ? bitmaps[1] : null;

                if (leftBitmap == null) return;

                var leftWidth = (float)leftBitmap.Size.Width;
                var leftHeight = (float)leftBitmap.Size.Height;
                var rightWidth = rightBitmap == null ? 0f : (float)rightBitmap.Size.Width;
                var rightHeight = rightBitmap == null ? 0f : (float)rightBitmap.Size.Height;

                var maxHeight = Math.Max(leftHeight, rightHeight);
                pageRects.Add(new Rect(0, (maxHeight - leftHeight) / 2f, leftWidth, leftHeight));

                if (rightBitmap != null)
                    pageRects.Add(new Rect(leftWidth + spacing, (maxHeight - rightHeight) / 2f, 
                        rightWidth, rightHeight));

                contentSize = new Size(leftWidth + (rightBitmap == null ? 0f : rightWidth + spacing), 
                    maxHeight);
                break;
            }
            case LocalReaderMode.VerticalScrolling:
            {
                float maxWidth = 0f;
                float totalHeight = 0f;

                var items = new List<(Size Size, string Path)>();
                
                // 使用 allImagePaths 保持全局布局稳定性
                var pathsToUse = allImagePaths != null && allImagePaths.Count > 0 ? allImagePaths : (Sources ?? new List<string>());

                double totalKnownHeight = 0;
                int knownCount = 0;

                foreach (var path in pathsToUse)
                {
                    Size size = Size.Empty;
                    if (bitmapCache.TryGetValue(path, out var bmp))
                    {
                        size = bmp.Size;
                        _pageSizeCache[path] = size;
                    }
                    else if (_pageSizeCache.TryGetValue(path, out var cachedSize))
                    {
                        size = cachedSize;
                    }

                    if (!size.IsEmpty)
                    {
                        maxWidth = Math.Max(maxWidth, (float)size.Width);
                        totalKnownHeight += size.Height;
                        knownCount++;
                    }
                    items.Add((size, path));
                }

                // 计算估算高度
                float estimatedHeight = knownCount > 0 ? (float)(totalKnownHeight / knownCount) : (maxWidth > 0 ? maxWidth * 1.414f : 1000f);
                float layoutWidth = maxWidth > 0 ? maxWidth : 1000f;

                foreach (var item in items)
                {
                    if (item.Size.IsEmpty)
                    {
                        // 未知尺寸，使用估算高度占位，确保滚动条和锚点逻辑正常工作
                        pageRects.Add(new Rect(0, totalHeight, layoutWidth, estimatedHeight));
                        totalHeight += estimatedHeight + spacing;
                    }
                    else
                    {
                        var width = (float)item.Size.Width;
                        var height = (float)item.Size.Height;
                        var x = (layoutWidth - width) / 2f;
                        pageRects.Add(new Rect(x, totalHeight, width, height));
                        totalHeight += height + spacing;
                    }
                }

                if (pageRects.Count > 0)
                    totalHeight -= spacing;

                contentSize = new Size(layoutWidth, Math.Max(0f, totalHeight));
                break;
            }
            default: // SinglePage
            {
                var bitmap = bitmaps[0];
                var width = (float)bitmap.Size.Width;
                var height = (float)bitmap.Size.Height;
                pageRects.Add(new Rect(0, 0, width, height));
                contentSize = new Size(width, height);
                break;
            }
        }
    }

    #endregion

}
