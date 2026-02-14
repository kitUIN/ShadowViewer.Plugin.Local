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
            case NotifyCollectionChangedAction.Reset:
                ReloadItems();
                break;
            // Remove, Replace, Move 不再单独处理，统一走 Reset
            default:
                ReloadItems();
                break;
        }
    }

    /// <summary>
    /// 异步加载队列，用于顺序加载节点尺寸，避免并发。
    /// </summary>
    private readonly Queue<RenderNode> pendingSizeLoadQueue = new();

    /// <summary>
    /// 是否正在处理尺寸加载队列。
    /// </summary>
    private bool isProcessingSizeQueue = false;

    /// <summary>
    /// 异步加载尺寸时是否需要更新布局。
    /// </summary>
    private bool sizeLoadPendingLayout = false;

    private void AddItems(System.Collections.IList? newItems, int startIndex)
    {
        if (newItems == null || newItems.Count == 0) return;

        var newNodes = new List<RenderNode>(newItems.Count);

        // 1. 快速创建占位节点，不加载实际数据
        foreach (var item in newItems)
        {
            var ctx = new ImageLoadingContext { Source = item, Size = new Size(200, 300) };
            var node = new RenderNode
            {
                PageIndex = -1,
                Source = item,
                Ctx = ctx,
                Bounds = new Rect(0, 0, 200, 300) // 默认占位尺寸
            };
            newNodes.Add(node);
        }

        // 2. 同步插入到列表
        lock (allNodes)
        {
            if (startIndex < 0 || startIndex > allNodes.Count)
                startIndex = allNodes.Count;

            allNodes.InsertRange(startIndex, newNodes);

            // 更新页码索引
            for (int i = startIndex; i < allNodes.Count; i++)
            {
                allNodes[i].PageIndex = i;
            }

            TotalPage = allNodes.Count;

            // 3. 将新节点加入尺寸加载队列
            foreach (var node in newNodes)
            {
                pendingSizeLoadQueue.Enqueue(node);
            }
        }

        // 4. 触发布局更新（批量模式下延迟更新）
        if (isBatchAdding)
        {
            hasPendingLayoutUpdate = true;
        }
        else
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateActiveLayout();
            });
        }

        // 5. 启动尺寸加载队列处理
        _ = ProcessSizeLoadQueueAsync();
    }

    /// <summary>
    /// 顺序处理尺寸加载队列，避免并发加载造成卡顿。
    /// </summary>
    private async Task ProcessSizeLoadQueueAsync()
    {
        if (isProcessingSizeQueue) return;
        isProcessingSizeQueue = true;

        try
        {
            while (true)
            {
                RenderNode? node;
                lock (allNodes)
                {
                    if (pendingSizeLoadQueue.Count == 0)
                        break;
                    node = pendingSizeLoadQueue.Dequeue();
                }

                if (node != null)
                {
                    await LoadNodeSizeAsync(node);
                }

                // 让出时间片，保持 UI 响应
                await Task.Yield();
            }
        }
        finally
        {
            isProcessingSizeQueue = false;

            // 队列处理完成后，如有需要则更新布局
            if (sizeLoadPendingLayout)
            {
                sizeLoadPendingLayout = false;
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateActiveLayout();
                });
            }
        }
    }

    /// <summary>
    /// 异步加载节点尺寸（只获取尺寸，不加载完整图片数据）。
    /// </summary>
    private async Task LoadNodeSizeAsync(RenderNode node)
    {
        var strategy = ImageStrategies.FirstOrDefault(s => s.CanHandle(node.Source));
        if (strategy == null) return;

        try
        {
            await strategy.InitImageAsync(node.Ctx);

            // 更新节点尺寸
            node.Bounds.Width = node.Ctx.Size.Width;
            node.Bounds.Height = node.Ctx.Size.Height;
            node.IsSizeLoaded = true;

            // 标记需要更新布局
            sizeLoadPendingLayout = true;
        }
        catch
        {
            // 加载失败保持默认尺寸
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
        if (d is not MangaReader { isUpdatingInternal: false } control || e.NewValue is not int newIndex) return;
        if (control.Mode == ReadingMode.VerticalScroll)
        {
        }

        control.UpdateActiveLayout();
        control.ResetZoom();
    }
}