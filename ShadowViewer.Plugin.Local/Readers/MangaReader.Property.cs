using System;
using Microsoft.UI.Xaml;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using ShadowViewer.Plugin.Local.Readers.Internal;

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
        if (d is MangaReader control && e.NewValue is float val)
        {
            control.pageSpacing = val;
            if (control.Mode == ReadingMode.VerticalScroll)
            {
                control.UpdateActiveLayout();
            }
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
    /// 前后预加载的页数。
    /// </summary>
    public static readonly DependencyProperty PreloadRangeProperty =
        DependencyProperty.Register(nameof(PreloadRange), typeof(int), typeof(MangaReader),
            new PropertyMetadata(4, OnPreloadRangeChanged));

    /// <summary>
    /// 获取或设置前后预加载的页数。
    /// </summary>
    public int PreloadRange
    {
        get => (int)GetValue(PreloadRangeProperty);
        set => SetValue(PreloadRangeProperty, value);
    }

    private static void OnPreloadRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MangaReader control && e.NewValue is int val)
        {
            control.preloadRange = val;
        }
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

    /// <summary>
    /// 是否强制图片符合众数尺寸的依赖属性键。
    /// </summary>
    public static readonly DependencyProperty IsFitToModeSizeProperty =
        DependencyProperty.Register(nameof(IsFitToModeSize), typeof(bool), typeof(MangaReader),
            new PropertyMetadata(true, OnIsFitToModeSizeChanged));

    /// <summary>
    /// 是否强制所有图片在绘制时符合众数高度与众数宽度（强制统一页面大小）。
    /// </summary>
    public bool IsFitToModeSize
    {
        get => (bool)GetValue(IsFitToModeSizeProperty);
        set => SetValue(IsFitToModeSizeProperty, value);
    }

    private static void OnIsFitToModeSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MangaReader control)
        {
            control.UpdateActiveLayout();
        }
    }

    /// <summary>
    /// 是否允许在垂直滚动模式下进行水平拖拽的依赖属性键。
    /// </summary>
    public static readonly DependencyProperty AllowHorizontalDragInScrollModeProperty =
        DependencyProperty.Register(nameof(AllowHorizontalDragInScrollMode), typeof(bool), typeof(MangaReader),
            new PropertyMetadata(false, OnAllowHorizontalDragInScrollModeChanged));

    /// <summary>
    /// 指示在垂直滚动模式下是否允许水平拖拽。默认为 <c>false</c>，即仅允许上下拖动。
    /// 设置为 <c>true</c> 后可自由上下左右拖拽。
    /// </summary>
    public bool AllowHorizontalDragInScrollMode
    {
        get => (bool)GetValue(AllowHorizontalDragInScrollModeProperty);
        set => SetValue(AllowHorizontalDragInScrollModeProperty, value);
    }

    private static void OnAllowHorizontalDragInScrollModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MangaReader control || e.NewValue is not bool val) return;
        control.allowHorizontalDragInScrollMode = val;
        if (control.Mode != ReadingMode.VerticalScroll || val) return;
        control.state.CameraPos.X = 0;
        control.state.Velocity.X = 0;
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
    public object? ItemsSource
    {
        get =>  GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MangaReader control) return;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnItemsSourceCollectionChanged;
        }
        if (e.NewValue is not INotifyCollectionChanged newCollection) return;
        newCollection.CollectionChanged += control.OnItemsSourceCollectionChanged;
        control.ReloadItems();
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
    /// 异步加载尺寸时是否需要更新布局。
    /// </summary>
    private bool sizeLoadPendingLayout = false;

    /// <summary>
    /// 批次大小：每加载多少个节点后触发一次布局更新。
    /// </summary>
    private const int LayoutUpdateBatchSize = 10;

    /// <summary>
    /// 并发加载的最大消费者数量。
    /// </summary>
    private const int MaxConcurrentLoads = 2;

    /// <summary>
    /// 尺寸加载刷新防抖延迟（毫秒）。
    /// </summary>
    private const int SizeLoadLayoutFlushDelayMs = 120;

    /// <summary>
    /// 当前批次已加载的节点数量。
    /// </summary>
    private int currentBatchLoadedCount = 0;

    /// <summary>
    /// 标记是否已调度尺寸加载的防抖布局刷新任务。
    /// </summary>
    private int isSizeLoadFlushScheduled;

    /// <summary>
    /// 标记是否正在进行布局更新（用于防止页码变动）。
    /// </summary>
    private bool isLayoutUpdating = false;

    /// <summary>
    /// 标记用户是否正在交互（拖拽或滚动），用于防止布局更新时自动调整摄像机位置。
    /// </summary>
    private bool isUserInteracting = false;

    private void AddItems(IList? newItems, int startIndex)
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

            // 3. 将新节点加入尺寸加载通道
            foreach (var node in newNodes)
            {
                TryEnqueueSizeLoad(node);
            }
        }

        // 4. 触发布局更新（批量模式下延迟更新）
        if (isBatchAdding)
        {
            hasPendingLayoutUpdate = true;
        }
        else
        {
            this.DispatcherQueue.TryEnqueue(UpdateActiveLayout);
        }

    }

    /// <summary>
    /// 处理尺寸加载流水线请求。
    /// </summary>
    /// <param name="request">尺寸加载请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示尺寸加载处理的异步操作。</returns>
    private async Task ProcessSizeLoadRequestAsync(PipelineRequest<RenderNode> request, CancellationToken cancellationToken)
    {
        if (!sizeLoadPipeline.IsCurrentEpoch(request))
        {
            return;
        }

        // 尺寸已就绪时直接跳过，避免重复执行 InitImageAsync 带来的额外 CPU 与分配。
        if (request.Payload.IsSizeLoaded)
        {
            return;
        }

        await LoadNodeSizeAsync(request.Payload, cancellationToken);

        if (!request.Payload.IsSizeLoaded)
        {
            return;
        }

        int count = Interlocked.Increment(ref currentBatchLoadedCount);

        if (count % LayoutUpdateBatchSize == 0)
        {
            // 达到批次阈值后立即刷新，优先保障“连续新增内容可见”。
            Interlocked.Exchange(ref currentBatchLoadedCount, 0);
            sizeLoadPendingLayout = false;
            this.DispatcherQueue.TryEnqueue(UpdateLayoutWithPageLock);
        }
        else
        {
            sizeLoadPendingLayout = true;
            ScheduleSizeLoadLayoutFlush();
        }
    }

    /// <summary>
    /// 尝试将尺寸加载请求写入通道。
    /// </summary>
    /// <param name="node">待加载尺寸的节点。</param>
    /// <returns>写入成功返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    private bool TryEnqueueSizeLoad(RenderNode node)
    {
        return sizeLoadPipeline.TryEnqueue(node);
    }

    /// <summary>
    /// 调度尺寸加载后的防抖布局刷新。
    /// </summary>
    /// <returns>无返回值。</returns>
    private void ScheduleSizeLoadLayoutFlush()
    {
        if (Interlocked.Exchange(ref isSizeLoadFlushScheduled, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SizeLoadLayoutFlushDelayMs, deferredUiWorkCts.Token);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref isSizeLoadFlushScheduled, 0);
                return;
            }

            if (sizeLoadPendingLayout)
            {
                // 防抖刷新用于补齐“最后不足一个批次”的节点，避免它们长时间不参与布局。
                Interlocked.Exchange(ref currentBatchLoadedCount, 0);
                sizeLoadPendingLayout = false;
                this.DispatcherQueue.TryEnqueue(UpdateLayoutWithPageLock);
            }

            Interlocked.Exchange(ref isSizeLoadFlushScheduled, 0);
        });
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
    /// <param name="node">待加载尺寸的节点。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示尺寸加载操作的异步任务。</returns>
    private async Task LoadNodeSizeAsync(RenderNode node, CancellationToken cancellationToken)
    {
        var strategy = ImageStrategies.FirstOrDefault(s => s.CanHandle(node.Source));
        if (strategy == null) return;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await strategy.InitImageAsync(node.Ctx);
            cancellationToken.ThrowIfCancellationRequested();

            // 更新节点尺寸
            node.Bounds.Width = node.Ctx.Size.Width;
            node.Bounds.Height = node.Ctx.Size.Height;
            node.IsSizeLoaded = true;
            node.ImageStrategy = strategy;
        }
        catch (OperationCanceledException)
        {
            // 取消属于正常生命周期事件。
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