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
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using DryIoc;
using Microsoft.UI.Xaml;
using Serilog;

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
    /// 正在加载的页码集合（用于避免重复触发加载）。
    /// </summary>
    private HashSet<int> loadingPages = new HashSet<int>();

    /// <summary>
    /// 锁对象，用于同步访问加载状态集合。
    /// </summary>
    private object loadingLock = new object();

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
    public void ClearItems()
    {
        lock (allNodes)
        {
            foreach (var node in allNodes)
            {
                node.Dispose();
            }

            allNodes.Clear();
            TotalPage = 0;

            // 重置缓存
            layoutCache.ResetAfterClearItems();
        }

        lock (state.LayoutNodes)
        {
            state.LayoutNodes.Clear();
            state.CameraPos = Vector2.Zero;
            state.Zoom = 1.0f;
            state.Velocity = Vector2.Zero;
        }

        this.DispatcherQueue.TryEnqueue(UpdateActiveLayout);
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
    public async void SetItems(IEnumerable? items)
    {
        try
        {
            if (items == null)
            {
                ClearItems();
                return;
            }

            var itemList = new List<object>();
            foreach (var item in items) itemList.Add(item);

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
                    var batch = itemList.Skip(i).Take(batchSize).ToList();
                    AddItems(batch, -1);

                    // 让出时间片，保持 UI 响应
                    if (i + batchSize < totalCount)
                    {
                        await Task.Delay(1);
                    }
                }
            }
            finally
            {
                EndBatchAdd();
            }
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
        this.Unloaded += MangaReader_Unloaded;
    }

    private void MangaReader_Unloaded(object sender, RoutedEventArgs e)
    {
        if (mainCanvas != null)
        {
            mainCanvas.Paused = true;
            mainCanvas.RemoveFromVisualTree();
            mainCanvas = null;
        }
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

            EffectiveViewportChanged += (_, e) =>
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

            // 0. 处理输入增量 (平移与缩放)
            Vector2 deltaToApply = Vector2.Zero;
            float zoomToApply = 1.0f;
            Vector2 zoomCenter = Vector2.Zero;
            var inputDelta = inputController.ConsumeFrameDelta();
            deltaToApply = inputDelta.PanDelta;
            zoomToApply = inputDelta.ZoomDelta;
            zoomCenter = inputDelta.ZoomCenter;

            if (inputDelta.HasActivePointer)
            {
                lastPointerPos = inputDelta.ActivePointerPos;
            }

            // 应用缩放 (模拟 ManipulationDelta)
            if (Math.Abs(zoomToApply - 1.0f) > 0.0001f)
            {
                float oldZoom = state.Zoom;
                float minZoom = 0.1f * baseZoomScale;
                float maxZoom = 5.0f * baseZoomScale;
                state.Zoom = Math.Clamp(state.Zoom * zoomToApply, minZoom, maxZoom);

                // 缩放中心补偿 (World = (Screen - Center) / Zoom + Camera)
                state.CameraPos += (zoomCenter - viewSize / 2f) * (1.0f / oldZoom - 1.0f / state.Zoom);
                
                // 计算缩放速度用于惯性
                if (isDragging)
                {
                    state.ZoomVelocity = (zoomToApply - 1.0f) / dt;
                    inputController.LastZoomCenter = zoomCenter;
                }
            }

            // 应用平移
            if (deltaToApply != Vector2.Zero)
            {
                bool isZoomed = Math.Abs(state.Zoom - baseZoomScale) > 0.001f;
                bool isSpreadMode = state.CurrentMode == ReadingMode.SpreadLtr || state.CurrentMode == ReadingMode.SpreadRtl;
                
                // 只有在非双页模式，或者已缩放的双页模式下才允许平移
                bool canDrag = !isSpreadMode || isZoomed;

                if (canDrag)
                {
                    if (state.CurrentMode == ReadingMode.VerticalScroll && !allowHorizontalDragInScrollMode && !isZoomed)
                    {
                        deltaToApply.X = 0;
                        state.Velocity = Vector2.Zero;
                    }
                    else
                    {
                        // 计算实时速度用于惯性开始
                        if (isDragging)
                        {
                            state.Velocity = -deltaToApply / state.Zoom / dt;
                        }
                    }
                    state.CameraPos -= deltaToApply / state.Zoom;

                }
                else
                {
                    // 在禁止平移的情况下（如双页未缩放），清空速度防止意外惯性
                    state.Velocity = Vector2.Zero;
                }
            }

            // 1. 物理惯性
            if (!isDragging)
            {
                if (isAnimatingPageTurn)
                {
                    if (pageTurnAnimVelocity != 0)
                    {
                        // 将单步推进与收敛判定统一放在服务层，避免状态机细节散落在主循环。
                        var stepResult = pageTurnService.StepAnimation(
                            pageTurnAnimCurlAmount,
                            pageTurnAnimTargetCurl,
                            pageTurnAnimVelocity,
                            dt);

                        pageTurnAnimCurlAmount = stepResult.CurlAmount;
                        pageTurnAnimVelocity = stepResult.Velocity;

                        if (stepResult.IsFinished)
                        {
                            int targetIndex = pageTurnTargetIndex;
                            this.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (!isAnimatingPageTurn) return;

                                if (targetIndex != CurrentPageIndex)
                                {
                                    CurrentPageIndex = targetIndex;
                                }
                                isAnimatingPageTurn = false;
                            });
                        }
                    }
                }
                else
                {
                    // 平移惯性
                    if (state.Velocity.LengthSquared() > 0.001f)
                    {
                        state.CameraPos += state.Velocity * dt;
                        float decay = MathF.Exp(-state.Friction * dt);
                        state.Velocity *= decay;
                        if (state.Velocity.Length() < 1.0f) state.Velocity = Vector2.Zero;
                    }

                    // 缩放惯性
                    if (Math.Abs(state.ZoomVelocity) > 0.001f)
                    {
                        float oldZoom = state.Zoom;
                        float zoomStep = 1.0f + state.ZoomVelocity * dt;
                        
                        float minZoom = 0.1f * baseZoomScale;
                        float maxZoom = 5.0f * baseZoomScale;
                        state.Zoom = Math.Clamp(state.Zoom * zoomStep, minZoom, maxZoom);

                        // 缩放中心补偿 (使用释放时的中心点)
                        state.CameraPos += (inputController.LastZoomCenter - viewSize / 2f) * (1.0f / oldZoom - 1.0f / state.Zoom);

                        float decay = MathF.Exp(-state.Friction * dt);
                        state.ZoomVelocity *= decay;
                        if (Math.Abs(state.ZoomVelocity) < 0.01f) state.ZoomVelocity = 0;
                    }
                }
            }

            // 2. 资源管理
            UpdateVisibleNodes(sender);

            // 3. 计算当前页
            UpdateCurrentPage();

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
    private void UpdateCurrentPage()
    {
        // 如果正在更新布局，跳过页码计算（防止页码变动）
        if (isLayoutUpdating) return;

        // 简单的距离计算来确定当前页
        int total;
        int current = 1;

        lock (allNodes)
        {
            total = allNodes.Count;
        }

        lock (state.LayoutNodes)
        {
            var visibleNodes = state.LayoutNodes;
            if (visibleNodes.Count == 0) return;

            float minDistance = float.MaxValue;
            RenderNode? closestNode = null;

            foreach (var node in visibleNodes)
            {
                var centerX = (float)(node.Bounds.X + node.Bounds.Width / 2);
                var centerY = (float)(node.Bounds.Y + node.Bounds.Height / 2);
                var center = new Vector2(centerX, centerY);

                var dist = Vector2.DistanceSquared(center, state.CameraPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestNode = node;
                }
            }

            if (closestNode != null)
            {
                current = closestNode.PageIndex + 1;
            }
        }

        float currentZoom = state.Zoom;
        float relativeZoom = baseZoomScale > 0 ? currentZoom / baseZoomScale : 1.0f;

        bool zoomChanged = Math.Abs(relativeZoom - lastReportedZoom) > 0.001f;
        bool pageChanged = current != lastReportedPage || total != lastReportedTotal;

        if (pageChanged || zoomChanged)
        {
            lastReportedPage = current;
            lastReportedTotal = total;
            lastReportedZoom = relativeZoom;

            this.DispatcherQueue.TryEnqueue(() =>
            {
                // 更新页码
                int newIndex = current - 1;
                if (newIndex != CurrentPageIndex)
                {
                    isUpdatingInternal = true;
                    CurrentPageIndex = newIndex;
                    isUpdatingInternal = false;
                }

                // 更新页码显示
                if (pageInfoText != null)
                {
                    pageInfoText.Text = $"{current} / {total}";
                }

                // 更新缩放
                if (Math.Abs(ZoomFactor - relativeZoom) > 0.001f)
                {
                    ZoomFactor = relativeZoom;
                }

                if (TotalPage != total)
                {
                    TotalPage = total;
                }
            });
        }
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

            // 绘制节点
            lock (state.LayoutNodes)
            {
                bool isCurling = (state.CurrentMode == ReadingMode.SpreadLtr || state.CurrentMode == ReadingMode.SpreadRtl) 
                                 && Math.Abs(state.Zoom - baseZoomScale) <= 0.001f 
                                 && ((isDragging && inputController.ActivePointerCount == 1) || isAnimatingPageTurn);
                
                RenderNode? curlingNode = null;
                bool curlFromRight = false;
                float curlAmount = 0;

                if (isCurling)
                {
                    if (isAnimatingPageTurn)
                    {
                        curlFromRight = pageTurnCurlFromRight;
                        curlAmount = pageTurnAnimCurlAmount;
                        curlingNode = pageTurnCurlingNode;
                    }
                    else
                    {
                        float dragDeltaX = lastPointerPos.X - inputController.DragStartPos.X;
                        if (dragDeltaX < -10) // Drag left
                        {
                            curlFromRight = true;
                            curlAmount = -dragDeltaX / state.Zoom;
                            curlingNode = state.LayoutNodes.OrderByDescending(n => n.Bounds.X).FirstOrDefault();
                        }
                        else if (dragDeltaX > 10) // Drag right
                        {
                            curlFromRight = false;
                            curlAmount = dragDeltaX / state.Zoom;
                            curlingNode = state.LayoutNodes.OrderBy(n => n.Bounds.X).FirstOrDefault();
                        }
                    }
                }

                // Draw non-curling nodes first
                foreach (var node in state.LayoutNodes)
                {
                    if (node == curlingNode) continue;
                    
                    if (IsIntersecting(viewportRect, node.Bounds))
                    {
                        DrawNodeNormal(ds, node);
                    }
                }

                // Draw the curling node and the node underneath
                if (curlingNode != null)
                {
                    // Find the node underneath
                    RenderNode? nodeUnderneath = null;
                    RenderNode? nodeBack = null;
                    
                    int nextIndex = curlingNode.PageIndex + (curlFromRight ? 2 : -2);
                    int nextBackIndex = curlingNode.PageIndex + (curlFromRight ? 1 : -1);
                    
                    lock (allNodes)
                    {
                        if (nextIndex >= 0 && nextIndex < allNodes.Count)
                        {
                            nodeUnderneath = allNodes[nextIndex];
                        }
                        if (nextBackIndex >= 0 && nextBackIndex < allNodes.Count)
                        {
                            nodeBack = allNodes[nextBackIndex];
                        }
                    }
                    
                    // Draw the node underneath at the curling node's position
                    if (nodeUnderneath != null)
                    {
                        bool drewUnder = false;
                        
                        nodeUnderneath.UseBitmap(bitmap =>
                        {
                            if (bitmap != null)
                            {
                                ds.DrawImage(bitmap, nodeUnderneath.Bounds);
                                drewUnder = true;
                            }
                        });
                        if (!drewUnder)
                        {
                            ds.DrawRectangle(curlingNode.Bounds, Windows.UI.Color.FromArgb(255, 50, 50, 50));
                        }
                        
                    }

                    DrawCurledPage(ds, curlingNode, nodeBack, curlAmount, curlFromRight);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"MainCanvas_Draw Error: {ex}");
        }
    }

    private void DrawNodeNormal(CanvasDrawingSession ds, RenderNode node)
    {
        bool drew = false;
        node.UseBitmap(bitmap =>
        {
            try
            {
                if (bitmap != null)
                {
                    ds.DrawImage(bitmap, node.Bounds);
                    drew = true;
                }
            }
            catch { }
        });

        if (!drew)
        {
            ds.DrawRectangle(node.Bounds, Windows.UI.Color.FromArgb(255, 100, 100, 100));
            using var format = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat();
            format.FontSize = 24;
            format.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Center;
            format.VerticalAlignment = Microsoft.Graphics.Canvas.Text.CanvasVerticalAlignment.Center;
            ds.DrawText($"{node.PageIndex + 1}", node.Bounds, Windows.UI.Color.FromArgb(255, 200, 200, 200), format);
        }
    }

    private void DrawCurledPage(CanvasDrawingSession ds, RenderNode node, RenderNode? nodeUnderneath, float curlAmount, bool curlFromRight)
    {
        if (curlAmount <= 0)
        {
            DrawNodeNormal(ds, node);
            return;
        }

        float W = (float)node.Bounds.Width;
        float H = (float)node.Bounds.Height;
        float X = (float)node.Bounds.X;
        float Y = (float)node.Bounds.Y;

        float R = (float)Math.Min(40.0, curlAmount / Math.PI);
        if (R < 1.0f) R = 1.0f;
        float L = (float)(curlAmount / 2.0 + Math.PI * R / 2.0);

        bool drew = false;
        
        CanvasBitmap? frontBitmap = null;
        CanvasBitmap? backBitmap = null;

        node.UseBitmap(b => frontBitmap = b);
        nodeUnderneath?.UseBitmap(b => backBitmap = b);

        if (frontBitmap == null) 
        {
            DrawNodeNormal(ds, node);
            return;
        }
        drew = true;

        using var spriteBatch = ds.CreateSpriteBatch(CanvasSpriteSortMode.None, CanvasImageInterpolation.Linear, CanvasSpriteOptions.None);

        float stripWidth = 2.0f;
        int numStrips = (int)Math.Ceiling(W / stripWidth);

        Action<int> drawStrip = (i) =>
        {
            float x = i * stripWidth;
            float currentStripWidth = Math.Min(stripWidth, W - x);
            if (currentStripWidth <= 0) return;

            float x_prime = 0;
            float scaleX = 1;
            float shade = 1.0f;

            if (curlFromRight)
            {
                float curlX = W - L;
                float d = x - curlX;

                if (d <= 0)
                {
                    x_prime = x;
                    scaleX = 1;
                    shade = 1.0f;
                }
                else if (d < Math.PI * R)
                {
                    float alpha = d / R;
                    x_prime = curlX + R * (float)Math.Sin(alpha);
                    scaleX = (float)Math.Cos(alpha);
                    shade = 1.0f - 0.3f * (float)Math.Sin(alpha);
                }
                else
                {
                    x_prime = curlX - (d - (float)Math.PI * R);
                    scaleX = -1;
                    shade = 0.6f;
                }
            }
            else
            {
                float curlX = L;
                float d = curlX - x;

                if (d <= 0)
                {
                    x_prime = x;
                    scaleX = 1;
                    shade = 1.0f;
                }
                else if (d < Math.PI * R)
                {
                    float alpha = d / R;
                    x_prime = curlX - R * (float)Math.Sin(alpha);
                    scaleX = (float)Math.Cos(alpha);
                    shade = 1.0f - 0.3f * (float)Math.Sin(alpha);
                }
                else
                {
                    x_prime = curlX + (d - (float)Math.PI * R);
                    scaleX = -1;
                    shade = 0.6f;
                }
            }

            if (Math.Abs(scaleX) < 0.001f) return;

            bool isBack = scaleX < 0;
            CanvasBitmap? currentBitmap = isBack && backBitmap != null ? backBitmap : frontBitmap;
            
            if (currentBitmap == null) return;

            float drawScaleX = scaleX;
            float destX = x_prime;
            float sourceX = x;

            if (isBack && backBitmap != null)
            {
                drawScaleX = -scaleX;
                destX = x_prime - currentStripWidth * drawScaleX;
                sourceX = W - x - currentStripWidth;
            }

            Rect sourceRect = new Rect(
                (sourceX / W) * currentBitmap.Size.Width,
                0,
                (currentStripWidth / W) * currentBitmap.Size.Width,
                currentBitmap.Size.Height
            );

            if (sourceRect.Width <= 0 || sourceRect.Height <= 0) return;

            float scaleX_total = (currentStripWidth * drawScaleX) / (float)sourceRect.Width;
            float scaleY_total = H / (float)sourceRect.Height;

            Matrix3x2 finalTransform = Matrix3x2.CreateScale(scaleX_total, scaleY_total) * 
                                       Matrix3x2.CreateTranslation(X + destX, Y);

            Vector4 tint = new Vector4(shade, shade, shade, 1.0f);

            spriteBatch.DrawFromSpriteSheet(currentBitmap, finalTransform, sourceRect, tint);
        };

        if (curlFromRight)
        {
            for (int i = 0; i < numStrips; i++) drawStrip(i);
        }
        else
        {
            for (int i = numStrips - 1; i >= 0; i--) drawStrip(i);
        }

        if (!drew)
        {
            DrawNodeNormal(ds, node);
        }
    }

    /// <summary>
    /// 从字节数组创建 Win2D 的 <see cref="CanvasBitmap"/>。若输入为空或失败返回 <c>null</c>。
    /// </summary>
    /// <param name="bytes">图像字节数组。</param>
    /// <param name="device">Canvas 设备引用。</param>
    /// <returns>加载成功的 <see cref="CanvasBitmap"/> 或 <c>null</c>。</returns>
    private static async Task<CanvasBitmap?> GetBitmap(byte[]? bytes, CanvasDevice device)
    {
        if (bytes == null || bytes.Length == 0) return null;

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            return await CanvasBitmap.LoadAsync(device, stream);
        }
        catch (Exception)
        {
            return null;
        }
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

        // 确定需要加载的节点集
        HashSet<RenderNode> nodesToLoad = new();
        int visibleMinIdx = int.MaxValue;
        int visibleMaxIdx = int.MinValue;

        lock (state.LayoutNodes)
        {
            foreach (var node in state.LayoutNodes)
            {
                // 1. 首先找出物理上真实可见的节点
                if (IsIntersecting(viewportRect, node.Bounds))
                {
                    nodesToLoad.Add(node);
                    if (node.PageIndex < visibleMinIdx) visibleMinIdx = node.PageIndex;
                    if (node.PageIndex > visibleMaxIdx) visibleMaxIdx = node.PageIndex;
                }
            }
        }

        // 2. 统一预加载逻辑：根据可见页码范围，前后各扩展 PreloadRange 页
        if (visibleMinIdx != int.MaxValue)
        {
            int range = preloadRange;
            lock (allNodes)
            {
                int start = Math.Max(0, visibleMinIdx - range);
                int end = Math.Min(allNodes.Count - 1, visibleMaxIdx + range);
                for (int i = start; i <= end; i++)
                {
                    nodesToLoad.Add(allNodes[i]);
                }
            }
        }

        // 为了避免在 Update 线程中执行耗时操作，这里的逻辑要尽量快
        // 遍历所有节点应用加载/卸载
        lock (allNodes)
        {
            foreach (var node in allNodes)
            {
                if (nodesToLoad.Contains(node))
                {
                    lock (loadingLock)
                    {
                        if (!node.IsLoaded && loadingPages.Add(node.PageIndex))
                        {
                            _ = Task.Run(() => LoadBitMap(node, device));
                        }
                    }
                }
                else
                {
                    if (!node.IsLoaded) continue;
                    lock (loadingLock)
                    {
                        if (!loadingPages.Contains(node.PageIndex))
                        {
                            node.Dispose();
                        }
                    }
                }
            }
        }
    }

    private async void LoadBitMap(RenderNode? node, CanvasDevice? device)
    {
        try
        {
            // ImageStrategy为null说明还未初始化
            if (node?.ImageStrategy == null) return;
            if (!node.Preloaded)
            {
                await node.ImageStrategy.PreloadImageAsync(node.Ctx);
                node.Preloaded = true;
            }

            if (device == null) return;
            var bitmap = await GetBitmap(node.Ctx.Bytes, device);
            node.SetBitmap(bitmap);
        }
        catch (Exception ex)
        {
            Log.Error($"LoadBitMap Error: {ex}");
        }
        finally
        {
            lock (loadingLock)
            {
                loadingPages.Remove(node.PageIndex);
            }
        }
    }

    /// <summary>
    /// 简单的矩形相交测试，用于视口剔除。
    /// </summary>
    private bool IsIntersecting(Rect a, Rect b)
    {
        return a.X < b.X + b.Width &&
               a.X + a.Width > b.X &&
               a.Y < b.Y + b.Height &&
               a.Y + a.Height > b.Y;
    }

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