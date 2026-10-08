using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Readers.Internal;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using DryIoc;
using Microsoft.UI.Xaml;
using Serilog;
using ShadowViewer.Plugin.Local.Readers.Rendering;

namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 漫画阅读器控件，负责加载图片资源、布局页面、处理输入以及使用 Win2D 绘制内容。
/// 该类为控件的核心实现，管理渲染节点、摄像机状态、缩放与惯性滚动逻辑。
/// </summary>
public sealed partial class MangaReader : Control
{
    /// <summary>
    /// 主绘制画布（Win2D CanvasAnimatedControl）的引用。
    /// </summary>
    private CanvasAnimatedControl? mainCanvas;

    /// <summary>
    /// 
    /// </summary>
    private Grid? rootGrid;

    /// <summary>
    /// EffectiveViewportChanged 事件处理器引用，用于重复模板应用与卸载时可靠解绑。
    /// </summary>
    private TypedEventHandler<FrameworkElement, EffectiveViewportChangedEventArgs>? effectiveViewportChangedHandler;

    /// <summary>
    /// 渲染引擎状态，包含摄像机位置、缩放、速度与布局节点等信息。
    /// </summary>
    private readonly EngineState state = new();

    /// <summary>
    /// 当前是否处于拖拽（抓取）状态，用于控制惯性滚动等行为。
    /// </summary>
    private bool isDragging;

    /// <summary>
    /// 上一次指针位置（屏幕坐标）。
    /// </summary>
    private Vector2 lastPointerPos;

    /// <summary>
    /// 当前捕获的指针 ID。
    /// </summary>
    private int pointerId = -1;

    /// <summary>
    /// 当前视口尺寸（像素）。
    /// </summary>
    private Vector2 viewSize = Vector2.Zero;

    // Store all loaded nodes

    /// <summary>
    /// 所有已创建的渲染节点（包含未布局或已布局的所有页节点）。
    /// </summary>
    private readonly List<RenderNode> allNodes = new List<RenderNode>();

    // 防止重复加载

    /// <summary>
    /// 图片滑动窗口加载控制器。
    /// </summary>
    private readonly ReaderImageWindowLoadController imageWindowLoadController = new();
    private readonly ReaderImageLoadService imageLoadService;

    /// <summary>
    /// 位图加载后台流水线。
    /// </summary>
    private readonly ReaderBackgroundPipeline<ReaderImageLoadTicket> bitmapLoadPipeline;

    /// <summary>
    /// 尺寸加载后台流水线。
    /// </summary>
    private readonly ReaderBackgroundPipeline<RenderNode> sizeLoadPipeline;

    /// <summary>
    /// 延迟 UI 任务取消令牌源（用于防抖刷新等延迟任务）。
    /// </summary>
    private CancellationTokenSource deferredUiWorkCts = new();

    /// <summary>
    /// SetItems 请求替换锁，确保取消与令牌替换过程原子化。
    /// </summary>
    private readonly object setItemsRequestLock = new();

    /// <summary>
    /// 当前 SetItems 请求的取消令牌源。
    /// </summary>
    private CancellationTokenSource? setItemsCts;

    /// <summary>
    /// 串行化 SetItems 流程，避免多个请求并发交错写入节点集合。
    /// </summary>
    private readonly SemaphoreSlim setItemsGate = new(1, 1);

    /// <summary>
    /// SetItems 请求版本号，用于快速丢弃过期请求。
    /// </summary>
    private int setItemsVersion;

    /// <summary>
    /// 标记当前是否处于内部更新流程中，以避免属性回调触发循环。
    /// </summary>
    private bool isUpdatingInternal;

    /// <summary>
    /// 上次报告的缩放值（用于减少不必要的 UI 更新）。
    /// </summary>
    private float lastReportedZoom = -1f;

    /// <summary>
    /// 基准缩放比例（100% 对应的实际缩放值）。
    /// </summary>
    private float baseZoomScale = 1.0f; // 基准缩放比例 (100% 对应的实际缩放值)

    // 页码信息

    /// <summary>
    /// 上次报告的当前页（用于检测页码变化）。
    /// </summary>
    private int lastReportedPage = -1;

    /// <summary>
    /// 上次报告的总页数（用于检测变化）。
    /// </summary>
    private int lastReportedTotal = -1;

    /// <summary>
    /// 当前缓存的预加载页数，用于后台线程安全访问。
    /// </summary>
    private int preloadRange = 3;

    /// <summary>
    /// 当前缓存的页面间距，用于后台线程安全访问。
    /// </summary>
    private float pageSpacing = 0f;

    /// <summary>
    /// 当前缓存的是否允许水平拖拽，用于后台线程安全访问。
    /// </summary>
    private bool allowHorizontalDragInScrollMode = false;

    /// <summary>
    /// 是否处于批量添加模式，批量添加时延迟布局更新。
    /// </summary>
    private bool isBatchAdding = false;

    /// <summary>
    /// 批量添加期间是否有新节点需要更新布局。
    /// </summary>
    private bool hasPendingLayoutUpdate = false;

    /// <summary>
    /// 布局计算服务。
    /// </summary>
    private readonly ReaderLayoutService layoutService = new();

    /// <summary>
    /// 卷页判定与动画参数计算服务。
    /// </summary>
    private readonly PageTurnService pageTurnService = new();

    /// <summary>
    /// 帧状态编排器，负责输入与物理状态推进。
    /// </summary>
    private readonly ReaderFrameOrchestrator frameOrchestrator = new();

    /// <summary>
    /// UI 同步服务，负责计算页码与缩放快照。
    /// </summary>
    private readonly ReaderUiSyncService uiSyncService = new();

    /// <summary>
    /// 页面渲染器。
    /// </summary>
    private readonly IPageRenderer pageRenderer = new Win2DPageRenderer();

    /// <summary>
    /// 布局缓存状态。
    /// </summary>
    private readonly ReaderLayoutCacheState layoutCache = new();

    /// <summary>
    /// 缓存的众数高度。
    /// </summary>
    private double modeHeight => layoutCache.ModeHeight;

    /// <summary>
    /// 缓存的众数宽度。
    /// </summary>
    private double modeWidth => layoutCache.ModeWidth;

    // 翻页动画状态
    private readonly object pageTurnLock = new();
    private int pageTurnVersion;
    private bool isAnimatingPageTurn = false;
    private float pageTurnAnimCurlAmount = 0f;
    private float pageTurnAnimTargetCurl = 0f;
    private float pageTurnAnimVelocity = 0f;
    private int pageTurnTargetIndex = -1;
    private bool pageTurnCurlFromRight = false;
    private RenderNode? pageTurnCurlingNode = null;

    /// <summary>
    /// 插件可用的图像加载策略集合（优先级按添加顺序）。
    /// </summary>
    public IEnumerable<IImageSourceStrategy> ImageStrategies { get; }


    /// <summary>
    /// 清空所有节点并释放资源。
    /// </summary>
    /// <param name="scheduleLayoutUpdate">是否在清理后调度一次布局更新。</param>
    public void ClearItems(bool scheduleLayoutUpdate = true)
    {
        CancelPageTurn(resetInput: true);
        // 清空内容时推进流水线世代，确保旧请求结果不会污染新数据集。
        imageWindowLoadController.Reset();
        sizeLoadPipeline.Invalidate();
        bitmapLoadPipeline.Invalidate();

        lock (allNodes)
        {
            foreach (var node in allNodes)
            {
                node.Retire();
            }

            allNodes.Clear();
            TotalPage = 0;

            // 重置缓存
            layoutCache.ResetAfterClearItems();
        }

        sizeLoadPendingLayout = false;
        Interlocked.Exchange(ref currentBatchLoadedCount, 0);

        lock (state.LayoutNodes)
        {
            state.LayoutNodes.Clear();
            state.CameraPos = Vector2.Zero;
            state.Zoom = 1.0f;
            state.Velocity = Vector2.Zero;
        }

        if (scheduleLayoutUpdate)
        {
            this.DispatcherQueue.TryEnqueue(UpdateActiveLayout);
        }
    }

    /// <summary>
    /// 开始批量添加模式，此模式下添加节点不会立即触发布局更新。
    /// </summary>
    public void BeginBatchAdd()
    {
        isBatchAdding = true;
        hasPendingLayoutUpdate = false;
    }

    /// <summary>
    /// 结束批量添加模式，统一触发布局更新。
    /// </summary>
    public void EndBatchAdd()
    {
        isBatchAdding = false;
        if (!hasPendingLayoutUpdate) return;
        this.DispatcherQueue.TryEnqueue(UpdateActiveLayout);
        hasPendingLayoutUpdate = false;
    }

    /// <summary>
    /// 添加单个项目到阅读器。
    /// </summary>
    /// <param name="item">要添加的项目。</param>
    public void AddItem(object item)
    {
        AddItems(new[] { item }, -1);
    }

    /// <summary>
    /// 添加多个项目到阅读器。
    /// </summary>
    /// <param name="items">要添加的项目集合。</param>
    public void AddItems(IEnumerable? items)
    {
        if (items == null) return;

        var itemList = new List<object>();
        foreach (var item in items) itemList.Add(item);

        if (itemList.Count == 0) return;

        BeginBatchAdd();
        AddItems(itemList, -1);
        EndBatchAdd();
    }

    /// <summary>
    /// 设置项目源，清空现有内容后添加所有项目。
    /// 这是推荐的加载方式：Clear -> Add 一个个。
    /// </summary>
    /// <param name="items">要设置的项目集合。</param>
    public void SetItems(IEnumerable? items)
    {
        CancellationTokenSource? oldCts;
        CancellationTokenSource newCts = new();
        int requestVersion;

        lock (setItemsRequestLock)
        {
            oldCts = setItemsCts;
            setItemsCts = newCts;
            requestVersion = Interlocked.Increment(ref setItemsVersion);
        }

        oldCts?.Cancel();
        oldCts?.Dispose();

        _ = SetItemsCoreAsync(items, requestVersion, newCts.Token);
    }

    /// <summary>
    /// 串行执行项目源切换，支持取消并防止旧请求覆盖新数据。
    /// </summary>
    /// <param name="items">要设置的项目集合。</param>
    /// <param name="requestVersion">当前请求版本号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示设置流程的异步任务。</returns>
    private async Task SetItemsCoreAsync(IEnumerable? items, int requestVersion, CancellationToken cancellationToken)
    {
        try
        {
            await setItemsGate.WaitAsync(cancellationToken);

            try
            {
            if (requestVersion != Volatile.Read(ref setItemsVersion))
            {
                return;
            }

            if (items == null)
            {
                ClearItems();
                return;
            }

            var itemList = new List<object>();
            foreach (var item in items) itemList.Add(item);

            cancellationToken.ThrowIfCancellationRequested();
            if (requestVersion != Volatile.Read(ref setItemsVersion)) return;

            ClearItems();

            if (itemList.Count == 0) return;

            // 异步分批添加，避免阻塞 UI
            const int batchSize = 50;
            var totalCount = itemList.Count;

            BeginBatchAdd();
            try
            {
                for (int i = 0; i < totalCount; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (requestVersion != Volatile.Read(ref setItemsVersion)) return;

                    var batch = itemList.Skip(i).Take(batchSize).ToList();
                    AddItems(batch, -1);

                    // 让出时间片，保持 UI 响应
                    if (i + batchSize < totalCount)
                    {
                        await Task.Delay(1, cancellationToken);
                    }
                }
            }
            finally
            {
                EndBatchAdd();
            }
            }
            finally
            {
                setItemsGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // 请求被新请求替换或控件卸载时取消属于正常行为。
        }
        catch (Exception ex)
        {
            Log.Error($"SetItems Error: {ex}");
        }
    }


    /// <summary>
    /// 创建 <see cref="MangaReader"/> 的新实例并初始化默认的图像加载策略。
    /// </summary>
    public MangaReader()
    {
        this.DefaultStyleKey = typeof(MangaReader);
        ImageStrategies = DiFactory.Services.ResolveMany<IImageSourceStrategy>();

        imageLoadService = new ReaderImageLoadService(imageWindowLoadController, GetBitmap,
            node => this.DispatcherQueue.TryEnqueue(() =>
            {
                if (!node.IsRetired) UpdateLayoutWithPageLock();
            }), ex => Log.Error($"LoadBitmap Error: {ex}"),
            (node, context, error) => node.ImageStrategy is NetworkStrategy network &&
                                     context.Bytes is not { Length: > 0 } && NetworkStrategy.IsCacheDecodeFailure(error)
                ? network.InvalidateImageCacheAsync(context) : Task.CompletedTask);

        bitmapLoadPipeline = new ReaderBackgroundPipeline<ReaderImageLoadTicket>(
            capacity: 256,
            workerCount: MaxConcurrentLoads,
            singleReader: false,
            singleWriter: false,
            processRequestAsync: ProcessBitmapLoadRequestAsync,
            onDiscarded: ticket => imageWindowLoadController.Complete(ticket, succeeded: false),
            onError: ex => Log.Error($"Bitmap pipeline Error: {ex}"));

        sizeLoadPipeline = new ReaderBackgroundPipeline<RenderNode>(
            capacity: 512,
            workerCount: MaxConcurrentLoads,
            singleReader: false,
            singleWriter: false,
            processRequestAsync: ProcessSizeLoadRequestAsync);

        bitmapLoadPipeline.Start();
        sizeLoadPipeline.Start();
        this.Loaded += MangaReader_Loaded;
        this.Unloaded += MangaReader_Unloaded;
    }

    /// <summary>
    /// 控件重新进入可视树时恢复后台流水线与延迟任务令牌。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">路由事件参数。</param>
    private void MangaReader_Loaded(object sender, RoutedEventArgs e)
    {
        if (deferredUiWorkCts.IsCancellationRequested)
        {
            deferredUiWorkCts.Dispose();
            deferredUiWorkCts = new CancellationTokenSource();
        }

        bitmapLoadPipeline.Start();
        sizeLoadPipeline.Start();
    }

    /// <summary>
    /// 控件卸载时停止画布并终止后台加载流水线。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">路由事件参数。</param>
    /// <returns>无返回值。</returns>
    private void MangaReader_Unloaded(object sender, RoutedEventArgs e)
    {
        CancelWheelInteractionClear();

        lock (setItemsRequestLock)
        {
            setItemsCts?.Cancel();
            setItemsCts?.Dispose();
            setItemsCts = null;
        }

        if (effectiveViewportChangedHandler != null)
        {
            EffectiveViewportChanged -= effectiveViewportChangedHandler;
            effectiveViewportChangedHandler = null;
        }

        if (mainCanvas != null)
        {
            mainCanvas.PointerPressed -= MainCanvas_PointerPressed;
            mainCanvas.PointerMoved -= MainCanvas_PointerMoved;
            mainCanvas.PointerReleased -= MainCanvas_PointerReleased;
            mainCanvas.PointerWheelChanged -= MainCanvas_PointerWheelChanged;
            mainCanvas.PointerCaptureLost -= MainCanvas_PointerCaptureLost;
            mainCanvas.PointerCanceled -= MainCanvas_PointerCanceled;
            mainCanvas.CreateResources -= MainCanvas_CreateResources;
            mainCanvas.Update -= MainCanvas_Update;
            mainCanvas.Draw -= MainCanvas_Draw;
            mainCanvas.Paused = true;
            mainCanvas.RemoveFromVisualTree();
            mainCanvas = null;
        }

        // 卸载时主动释放节点与位图，避免页面离开后仍持有大量图像内存。
        ClearItems(scheduleLayoutUpdate: false);

        // 卸载后停止后台流水线，避免控件已退出可视树时仍有异步回写。
        bitmapLoadPipeline.Stop();
        sizeLoadPipeline.Stop();
        deferredUiWorkCts.Cancel();
    }

    /// <summary>
    /// 在控件模板应用后获取模板部件并挂接画布事件处理器。
    /// </summary>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (mainCanvas != null)
        {
            mainCanvas.PointerPressed -= MainCanvas_PointerPressed;
            mainCanvas.PointerMoved -= MainCanvas_PointerMoved;
            mainCanvas.PointerReleased -= MainCanvas_PointerReleased;
            mainCanvas.PointerWheelChanged -= MainCanvas_PointerWheelChanged;
            mainCanvas.PointerCaptureLost -= MainCanvas_PointerCaptureLost;
            mainCanvas.PointerCanceled -= MainCanvas_PointerCanceled;
            mainCanvas.CreateResources -= MainCanvas_CreateResources;
            mainCanvas.Update -= MainCanvas_Update;
            mainCanvas.Draw -= MainCanvas_Draw;
        }

        if (effectiveViewportChangedHandler != null)
        {
            EffectiveViewportChanged -= effectiveViewportChangedHandler;
            effectiveViewportChangedHandler = null;
        }

        OnApplyZoomFlyoutTemplate();
        mainCanvas = GetTemplateChild("PART_MainCanvas") as CanvasAnimatedControl;
        rootGrid = GetTemplateChild("PART_RootGrid") as Grid;

        if (mainCanvas != null)
        {
            var brush = rootGrid?.Background as Microsoft.UI.Xaml.Media.SolidColorBrush;
            if (brush != null)
            {
                mainCanvas.ClearColor = brush.Color;
            }

            mainCanvas.PointerPressed += MainCanvas_PointerPressed;
            mainCanvas.PointerMoved += MainCanvas_PointerMoved;
            mainCanvas.PointerReleased += MainCanvas_PointerReleased;
            mainCanvas.PointerWheelChanged += MainCanvas_PointerWheelChanged;
            mainCanvas.PointerCaptureLost += MainCanvas_PointerCaptureLost;
            mainCanvas.PointerCanceled += MainCanvas_PointerCanceled;

            effectiveViewportChangedHandler = (_, e) =>
            {
                bool wasZero = viewSize == Vector2.Zero;
                viewSize = new Vector2((float)e.EffectiveViewport.Width, (float)e.EffectiveViewport.Height);
                if (Mode == ReadingMode.VerticalScroll)
                {
                    UpdateActiveLayout();
                }
                else if (wasZero)
                {
                    ResetZoom();
                }
            };
            EffectiveViewportChanged += effectiveViewportChangedHandler;

            mainCanvas.CreateResources += MainCanvas_CreateResources;
            mainCanvas.Update += MainCanvas_Update;
            mainCanvas.Draw += MainCanvas_Draw;
        }
    }

    /// <summary>
    /// Win2D 画布资源创建回调（目前保留空实现以备将来扩展）。
    /// </summary>
    private void MainCanvas_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
    {
    }

    /// <summary>
    /// 帧刷新的更新回调，负责物理惯性、可见节点管理、当前页检测与信息面板更新。
    /// </summary>
    private void MainCanvas_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
    {
        try
        {
            var dt = (float)args.Timing.ElapsedTime.TotalSeconds;
            var inputDelta = inputController.ConsumeFrameDelta();

            if (inputDelta.HasActivePointer)
            {
                lastPointerPos = inputDelta.ActivePointerPos;
            }

            // 1. 纯状态更新（输入 + 物理 + 卷页推进）
            ReaderFrameStepResult frameStep;
            int animationVersion;
            int targetIndex;
            lock (pageTurnLock)
            {
                animationVersion = pageTurnVersion;
                targetIndex = pageTurnTargetIndex;
                frameStep = frameOrchestrator.Step(
                    state,
                    inputDelta,
                    dt,
                    baseZoomScale,
                    viewSize,
                    isDragging,
                    allowHorizontalDragInScrollMode,
                    isAnimatingPageTurn,
                    pageTurnAnimCurlAmount,
                    pageTurnAnimTargetCurl,
                    pageTurnAnimVelocity,
                    inputController.LastZoomCenter,
                    pageTurnService);

                pageTurnAnimCurlAmount = frameStep.PageTurnAnimCurlAmount;
                pageTurnAnimVelocity = frameStep.PageTurnAnimVelocity;
                inputController.LastZoomCenter = frameStep.LastZoomCenter;
            }

            if (frameStep.PageTurnFinished)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    lock (pageTurnLock)
                    {
                        if (!isAnimatingPageTurn || animationVersion != pageTurnVersion) return;
                        CompletePageTurn(targetIndex);
                    }
                });
            }

            if (sizeLoadPendingLayout && !isDragging && !isAnimatingPageTurn) ScheduleSizeLoadLayoutFlush();

            // 2. 资源管理
            UpdateVisibleNodes(sender);

            // 3. 同步 DP/UI 数据
            SyncUiState();

            // 4. Update Info Panel
            UpdateInfoPanel();
        }
        catch (Exception ex)
        {
            // 防止更新过程中的异常导致程序崩溃
            Log.Error($"MainCanvas_Update Error: {ex}");
        }
    }

    /// <summary>
    /// 计算并更新当前页、缩放比例与总页数，必要时将这些信息调度到 UI 线程以更新依赖属性。
    /// </summary>
    private void SyncUiState()
    {
        int syncVersion;
        lock (pageTurnLock)
        {
            if (isAnimatingPageTurn) return;
            syncVersion = pageTurnVersion;
        }
        int total;
        lock (allNodes)
        {
            total = allNodes.Count;
        }

        List<RenderNode> layoutSnapshot;
        lock (state.LayoutNodes)
        {
            layoutSnapshot = state.LayoutNodes.ToList();
        }

        if (!uiSyncService.TryCreateSnapshot(
                isLayoutUpdating,
                layoutSnapshot,
                total,
                state.CameraPos,
                state.Zoom,
                baseZoomScale,
                lastReportedZoom,
                lastReportedPage,
                lastReportedTotal,
                out var snapshot))
        {
            return;
        }

        lastReportedPage = snapshot.CurrentPage;
        lastReportedTotal = snapshot.TotalPage;
        lastReportedZoom = snapshot.RelativeZoom;

        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (syncVersion != Volatile.Read(ref pageTurnVersion)) return;
            int newIndex = snapshot.CurrentPage - 1;
            if (newIndex != CurrentPageIndex)
            {
                isUpdatingInternal = true;
                CurrentPageIndex = newIndex;
                isUpdatingInternal = false;
            }

            if (pageInfoText != null)
            {
                pageInfoText.Text = $"{snapshot.CurrentPage} / {snapshot.TotalPage}";
            }

            if (Math.Abs(ZoomFactor - snapshot.RelativeZoom) > 0.001f)
            {
                ZoomFactor = snapshot.RelativeZoom;
            }

            if (TotalPage != snapshot.TotalPage)
            {
                TotalPage = snapshot.TotalPage;
            }
        });
    }

    /// <summary>
    /// Win2D 绘制回调：根据当前摄像机变换绘制布局节点或占位符。
    /// </summary>
    private void MainCanvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        try
        {
            var ds = args.DrawingSession;
            var size = viewSize;
            if (size == Vector2.Zero) return;

            var center = size / 2;
            var transform = Matrix3x2.CreateTranslation(-state.CameraPos) *
                            Matrix3x2.CreateScale(state.Zoom) *
                            Matrix3x2.CreateTranslation(center);

            ds.Transform = transform;

            // 计算视口 (用于剔除)
            if (!Matrix3x2.Invert(transform, out var inverseTransform)) return;
            var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
            var bottomRight = Vector2.Transform(size, inverseTransform);
            var viewportRect = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

            List<RenderNode> layoutSnapshot;
            lock (state.LayoutNodes)
            {
                layoutSnapshot = state.LayoutNodes.ToList();
            }

            List<RenderNode> allNodesSnapshot;
            lock (allNodes)
            {
                allNodesSnapshot = allNodes.ToList();
            }

            PageRenderContext renderContext;
            lock (pageTurnLock)
            {
                renderContext = new PageRenderContext(
                    ds,
                    viewportRect,
                    state.CurrentMode,
                    state.Zoom,
                    baseZoomScale,
                    isDragging,
                    isAnimatingPageTurn,
                    inputController.ActivePointerCount,
                    lastPointerPos,
                    inputController.DragStartPos,
                    pageTurnAnimCurlAmount,
                    pageTurnCurlFromRight,
                    pageTurnCurlingNode,
                    layoutSnapshot,
                    allNodesSnapshot,
                    inputController.IsPageTurnGesture);
            }

            pageRenderer.Draw(renderContext);
        }
        catch (Exception ex)
        {
            Log.Error($"MainCanvas_Draw Error: {ex}");
        }
    }

    /// <summary>
    /// 优先使用预加载 Hook 字节，否则从磁盘文件创建 Win2D 位图；异常由加载服务处理。
    /// </summary>
    /// <param name="ctx">请求上下文，包含预加载 Hook 字节或文件路径。</param>
    /// <param name="device">Canvas 设备引用。</param>
    /// <param name="token">加载请求取消令牌。</param>
    /// <returns>加载成功的 <see cref="CanvasBitmap"/> 或 <c>null</c>。</returns>
    private static async Task<CanvasBitmap?> GetBitmap(ImageLoadingContext ctx, CanvasDevice device, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // 预加载 Hook 可以用 Bytes 替换文件内容，因此优先采用它提供的字节。
        var bytes = ctx.Bytes;
        if (bytes is { Length: > 0 })
        {
            using var memoryStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(memoryStream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(token);
                await writer.FlushAsync().AsTask(token);
                writer.DetachStream();
            }
            memoryStream.Seek(0);
            return await CanvasBitmap.LoadAsync(device, memoryStream);
        }
        if (string.IsNullOrWhiteSpace(ctx.CachedFilePath)) return null;
        var file = await StorageFile.GetFileFromPathAsync(ctx.CachedFilePath).AsTask(token);
        using var fileStream = await file.OpenReadAsync().AsTask(token);
        // Win2D 解码完成后仍检查取消，并由加载服务释放无法发布的位图。
        return await CanvasBitmap.LoadAsync(device, fileStream);
    }

    /// <summary>
    /// 更新可见节点的加载状态：在可见或预加载区域内触发位图创建，移出视口则释放资源。
    /// </summary>
    private void UpdateVisibleNodes(ICanvasAnimatedControl sender)
    {
        var size = viewSize;
        if (size == Vector2.Zero) return;

        var center = size / 2;
        var transform = Matrix3x2.CreateTranslation(-state.CameraPos) *
                        Matrix3x2.CreateScale(state.Zoom) *
                        Matrix3x2.CreateTranslation(center);

        if (!Matrix3x2.Invert(transform, out var inverseTransform)) return;
        var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
        var bottomRight = Vector2.Transform(size, inverseTransform);
        var viewportRect = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        var device = sender.Device;

        List<RenderNode> layoutSnapshot;
        lock (state.LayoutNodes)
        {
            layoutSnapshot = state.LayoutNodes.ToList();
        }

        List<RenderNode> allNodesSnapshot;
        lock (allNodes)
        {
            allNodesSnapshot = allNodes.ToList();
        }

        imageWindowLoadController.UpdateWindow(
            layoutSnapshot,
            allNodesSnapshot,
            viewportRect,
            preloadRange,
            device,
            TryEnqueueBitmapLoad);
    }

    /// <summary>
    /// 尝试将位图加载请求写入后台流水线。
    /// </summary>
    /// <param name="ticket">当前窗口的节点加载请求。</param>
    /// <returns>写入成功返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    private bool TryEnqueueBitmapLoad(ReaderImageLoadTicket ticket) => bitmapLoadPipeline.TryEnqueue(ticket);

    /// <summary>处理位图加载请求；请求取消及结果发布由窗口加载服务统一管理。</summary>
    private Task ProcessBitmapLoadRequestAsync(PipelineRequest<ReaderImageLoadTicket> request, CancellationToken cancellationToken) =>
        imageLoadService.LoadAsync(request.Payload, cancellationToken);

    // --- 加载逻辑 ---

    /// <summary>
    /// 重新加载 ItemsSource 中的所有项。
    /// 简化实现：直接调用 SetItems 统一处理。
    /// </summary>
    private void ReloadItems()
    {
        SetItems(ItemsSource as IEnumerable);
    }

    /// <summary>
    /// 根据当前阅读模式和视口大小计算并更新 <see cref="EngineState.LayoutNodes"/> 中的布局信息。
    /// </summary>
    private void UpdateActiveLayout()
    {
        layoutService.UpdateActiveLayout(
            state,
            allNodes,
            CurrentPageIndex,
            pageSpacing,
            IsFitToModeSize,
            viewSize,
            layoutCache);
    }
}
