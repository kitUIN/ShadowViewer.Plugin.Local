using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
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

    /// <summary>
    /// 批次大小：每加载多少个节点后触发一次布局更新。
    /// </summary>
    private const int LayoutUpdateBatchSize = 10;

    /// <summary>
    /// 并发加载的最大线程数。
    /// </summary>
    private const int MaxConcurrentLoads = 4;

    /// <summary>
    /// 信号量，用于控制并发加载数量。
    /// </summary>
    private readonly System.Threading.SemaphoreSlim loadSemaphore = new(MaxConcurrentLoads, MaxConcurrentLoads);

    /// <summary>
    /// 当前批次已加载的节点数量。
    /// </summary>
    private int currentBatchLoadedCount = 0;

    /// <summary>
    /// 标记是否正在进行布局更新（用于防止页码变动）。
    /// </summary>
    private bool isLayoutUpdating = false;

    /// <summary>
    /// 标记用户是否正在交互（拖拽或滚动），用于防止布局更新时自动调整摄像机位置。
    /// </summary>
    private bool isUserInteracting = false;

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

        // 5. 启动尺寸加载队列处理（在后台线程）
        _ = Task.Run(() => ProcessSizeLoadQueueAsync());
    }

    /// <summary>
    /// 并发处理尺寸加载队列，使用信号量控制并发数量。
    /// 此方法在后台线程运行，不会阻塞 UI 线程。
    /// </summary>
    private async Task ProcessSizeLoadQueueAsync()
    {
        // 使用 volatile read 检查状态
        if (System.Threading.Volatile.Read(ref isProcessingSizeQueue)) return;
        
        lock (loadingLock)
        {
            if (isProcessingSizeQueue) return;
            isProcessingSizeQueue = true;
        }

        try
        {
            System.Threading.Interlocked.Exchange(ref currentBatchLoadedCount, 0);
            var loadTasks = new List<Task>();

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
                    // 启动并发加载任务
                    var loadTask = LoadNodeSizeWithSemaphoreAsync(node);
                    loadTasks.Add(loadTask);

                    // 如果达到最大并发数的2倍，等待一些任务完成
                    if (loadTasks.Count >= MaxConcurrentLoads * 2)
                    {
                        await Task.WhenAny(loadTasks);
                        loadTasks.RemoveAll(t => t.IsCompleted);
                    }
                }
            }

            // 等待所有加载任务完成
            if (loadTasks.Count > 0)
            {
                await Task.WhenAll(loadTasks);
            }
        }
        finally
        {
            lock (loadingLock)
            {
                isProcessingSizeQueue = false;
            }

            // 队列处理完成后，如果还有未达到批次大小的节点，也需要更新布局
            var count = System.Threading.Volatile.Read(ref currentBatchLoadedCount);
            if (count > 0 || sizeLoadPendingLayout)
            {
                System.Threading.Interlocked.Exchange(ref currentBatchLoadedCount, 0);
                sizeLoadPendingLayout = false;
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateLayoutWithPageLock();
                });
            }
        }
    }

    /// <summary>
    /// 使用信号量控制并发的节点尺寸加载。
    /// </summary>
    private async Task LoadNodeSizeWithSemaphoreAsync(RenderNode node)
    {
        await loadSemaphore.WaitAsync();
        try
        {
            await LoadNodeSizeAsync(node);
            
            // 每加载完成一个节点，增加计数
            if (node.IsSizeLoaded)
            {
                var count = System.Threading.Interlocked.Increment(ref currentBatchLoadedCount);

                // 达到批次大小时，立即触发布局更新
                if (count % LayoutUpdateBatchSize == 0)
                {
                    this.DispatcherQueue.TryEnqueue(UpdateLayoutWithPageLock);
                }
            }
        }
        finally
        {
            loadSemaphore.Release();
        }
    }

    /// <summary>
    /// 更新布局，同时保持当前页码不变（锁定摄像机到当前页面）。
    /// </summary>
    private void UpdateLayoutWithPageLock()
    {
        if (Mode != ReadingMode.VerticalScroll)
        {
            UpdateActiveLayout();
            ResetZoom(true);
            return;
        }

        // 如果用户正在交互（拖拽/滚动），只更新布局，不调整摄像机位置
        if (isUserInteracting)
        {
            // 标记正在更新布局，防止 UpdateCurrentPage 更新页码
            isLayoutUpdating = true;
            try
            {
                UpdateActiveLayout();
            }
            finally
            {
                isLayoutUpdating = false;
            }
            return;
        }

        // 保存当前页索引
        int targetPageIndex = CurrentPageIndex;
        
        // 在垂直滚动模式下，保存当前页面节点的中心点在世界坐标系中的位置
        RenderNode? targetNode = null;
        Vector2 oldNodeCenter = Vector2.Zero;
        
        lock (allNodes)
        {
            if (targetPageIndex >= 0 && targetPageIndex < allNodes.Count)
            {
                targetNode = allNodes[targetPageIndex];
                oldNodeCenter = new Vector2(
                    (float)(targetNode.Bounds.X + targetNode.Bounds.Width / 2),
                    (float)(targetNode.Bounds.Y + targetNode.Bounds.Height / 2)
                );
            }
        }

        // 标记正在更新布局，防止 UpdateCurrentPage 更新页码
        isLayoutUpdating = true;
        try
        {
            // 更新布局（包含重新计算缩放比例）
            UpdateActiveLayout();

            // 如果有目标节点，调整摄像机位置使其保持在相同位置
            if (targetNode != null)
            {
                Vector2 newNodeCenter = new Vector2(
                    (float)(targetNode.Bounds.X + targetNode.Bounds.Width / 2),
                    (float)(targetNode.Bounds.Y + targetNode.Bounds.Height / 2)
                );

                // 调整摄像机位置，补偿节点位置的变化
                Vector2 offset = newNodeCenter - oldNodeCenter;
                state.CameraPos += offset;
            }

            ResetZoom(true);
        }
        finally
        {
            isLayoutUpdating = false;
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
        if (d is not MangaReader { isUpdatingInternal: false } control || e.NewValue is not int) return;
        control.UpdateActiveLayout();
        control.ResetZoom();
    }
}