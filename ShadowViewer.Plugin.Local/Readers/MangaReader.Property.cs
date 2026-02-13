using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;

namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 漫画阅读器控件（部分），包含与页面源、分页/滚动模式、布局与缩放相关的依赖属性和处理逻辑。
/// 本部分实现属性声明、集合变更处理以及节点加载与布局更新的相关方法。
/// </summary>
public sealed partial class MangaReader
{
    /// <summary>
    /// 页面间距的依赖属性键。
    /// </summary>
    public static readonly DependencyProperty PageSpacingProperty =
        DependencyProperty.Register(nameof(PageSpacing), typeof(float), typeof(MangaReader),
            new PropertyMetadata(0.0f, OnPageSpacingChanged));

    /// <summary>
    /// 页面之间的间距（像素）。当处于滚动模式时，修改此值会触发布局更新。
    /// </summary>
    public float PageSpacing
    {
        get => (float)GetValue(PageSpacingProperty);
        set => SetValue(PageSpacingProperty, value);
    }

    private static void OnPageSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MangaReader control && control.Mode == ReadingMode.VerticalScroll)
        {
            control.UpdateActiveLayout();
        }
    }

    /// <summary>
    /// 阅读模式的依赖属性键。
    /// </summary>
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(nameof(Mode), typeof(ReadingMode), typeof(MangaReader),
            new PropertyMetadata(ReadingMode.VerticalScroll, OnModeChanged));

    /// <summary>
    /// 当前阅读模式（例如滚动或分页）。修改后会更新引擎状态并重置布局与缩放。
    /// </summary>
    public ReadingMode Mode
    {
        get => (ReadingMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>
    /// 是否启用鼠标滚轮进行页面导航的依赖属性键。
    /// </summary>
    public static readonly DependencyProperty EnableMouseWheelNavigationProperty =
        DependencyProperty.Register(nameof(EnableMouseWheelNavigation), typeof(bool), typeof(MangaReader),
            new PropertyMetadata(true));

    /// <summary>
    /// 指示是否启用使用鼠标滚轮进行页面切换或导航。
    /// </summary>
    public bool EnableMouseWheelNavigation
    {
        get => (bool)GetValue(EnableMouseWheelNavigationProperty);
        set => SetValue(EnableMouseWheelNavigationProperty, value);
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MangaReader control)
        {
            control.state.CurrentMode = (ReadingMode)e.NewValue;
            control.UpdateActiveLayout();
            control.ResetZoom();
            control.ScrollToPage(control.CurrentPageIndex);
        }
    }

    /// <summary>
    /// 当前页索引的依赖属性键。
    /// </summary>
    public static readonly DependencyProperty CurrentPageIndexProperty =
        DependencyProperty.Register(nameof(CurrentPageIndex), typeof(int), typeof(MangaReader),
            new PropertyMetadata(0, OnCurrentPageIndexChanged));

    /// <summary>
    /// 当前处于可视区或焦点的页索引（从 0 开始）。
    /// </summary>
    public int CurrentPageIndex
    {
        get => (int)GetValue(CurrentPageIndexProperty);
        set => SetValue(CurrentPageIndexProperty, value);
    }

    /// <summary>
    /// 总页数的依赖属性键。
    /// </summary>
    public static readonly DependencyProperty TotalPageProperty =
        DependencyProperty.Register(nameof(TotalPage), typeof(int), typeof(MangaReader),
            new PropertyMetadata(0));

    /// <summary>
    /// 当前源中的总页数（节点数量）。
    /// </summary>
    public int TotalPage
    {
        get => (int)GetValue(TotalPageProperty);
        set => SetValue(TotalPageProperty, value);
    }

    /// <summary>
    /// 图片或页面集合的源（可以是集合、列表或其他自定义对象）。
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(MangaReader),
            new PropertyMetadata(null, OnItemsSourceChanged));

    /// <summary>
    /// 获取或设置要在阅读器中显示的项目源。支持实现了 <see cref="INotifyCollectionChanged"/> 的集合以接收增量更新。
    /// </summary>
    public object ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MangaReader control)
        {
            control.ReloadItems();
            if (e.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnItemsSourceCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += control.OnItemsSourceCollectionChanged;
            }
        }
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                AddItems(e.NewItems, e.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveItems(e.OldItems, e.OldStartingIndex);
                break;
            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                ReloadItems();
                break;
        }
    }

    /// <summary>
    /// 清空当前节点并取消正在进行的加载操作，随后重置布局与页索引。
    /// </summary>
    private void ResetItems()
    {
        loadCts?.Cancel();
        lock (allNodes)
        {
            foreach (var node in allNodes)
            {
                node.Dispose();
            }

            allNodes.Clear();
            TotalPage = 0;
        }

        this.DispatcherQueue.TryEnqueue(() =>
        {
            UpdateActiveLayout();
            CurrentPageIndex = 0;
        });
    }

    private void AddItems(System.Collections.IList? newItems, int startIndex)
    {
        if (newItems == null) return;

        var newNodes = new List<RenderNode>();

        foreach (var item in newItems)
        {
            var ctx = new ImageLoadingContext { Source = item, Size = new Size(200, 300) };
            var node = new RenderNode
            {
                PageIndex = -1, // 稍后更新
                Source = item,
                Ctx = ctx,
                Bounds = new Rect(0, 0, 200, 300)
            };
            newNodes.Add(node);
        }

        // 2. 同步插入到列表 (Sync: Insert into list immediately)
        lock (allNodes)
        {
            if (startIndex < 0 || startIndex > allNodes.Count)
                startIndex = allNodes.Count;

            allNodes.InsertRange(startIndex, newNodes);

            // 更新页码索引
            // Update PageIndex
            for (int i = startIndex; i < allNodes.Count; i++)
            {
                allNodes[i].PageIndex = i;
            }

            TotalPage = allNodes.Count;

            // 3. 异步加载内容 (Async: Load content in background)
            foreach (var node in newNodes)
            {
                _ = LoadNodeDataAsync(node);
            }
        }
    }

    private void RemoveItems(System.Collections.IList? oldItems, int startIndex)
    {
        if (oldItems == null) return;
        lock (allNodes)
        {
            if (startIndex < 0 || startIndex >= allNodes.Count) return;

            int count = oldItems.Count;
            if (startIndex + count > allNodes.Count)
                count = allNodes.Count - startIndex;

            // Dispose removed nodes
            for (int i = 0; i < count; i++)
            {
                allNodes[startIndex + i].Dispose();
            }

            allNodes.RemoveRange(startIndex, count);

            // Update PageIndex
            for (int i = startIndex; i < allNodes.Count; i++)
            {
                allNodes[i].PageIndex = i;
            }

            TotalPage = allNodes.Count;
        }

        this.DispatcherQueue.TryEnqueue(() =>
        {
            UpdateActiveLayout();
            if (CurrentPageIndex >= TotalPage && TotalPage > 0)
            {
                CurrentPageIndex = TotalPage - 1;
            }
        });
    }

    private async Task LoadNodeDataAsync(RenderNode node)
    {
        var strategy = ImageStrategies.FirstOrDefault(s => s.CanHandle(node.Source));
        if (strategy == null) return;

        try
        {
            await strategy.InitImageAsync(node.Ctx);
            // Update bounds
            node.Bounds.Width = node.Ctx.Size.Width;
            node.Bounds.Height = node.Ctx.Size.Height;

            // Refresh Layout
            this.DispatcherQueue.TryEnqueue(UpdateActiveLayout);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// 页面缩放比例，作为额外的整体缩放系数使用。
    /// </summary>
    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(nameof(ZoomFactor), typeof(float), typeof(MangaReader),
            new PropertyMetadata(1.0f));

    /// <summary>
    /// 页面缩放比例，作为额外的整体缩放系数使用。
    /// </summary>
    public float ZoomFactor
    {
        get => (float)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    private static void OnCurrentPageIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MangaReader { isUpdatingInternal: false } control && e.NewValue is int newIndex)
        {
            if (control.Mode != ReadingMode.VerticalScroll)
            {
                control.UpdateActiveLayout();
                control.ResetZoom();
            }

            control.ScrollToPage(newIndex);
        }
    }
}