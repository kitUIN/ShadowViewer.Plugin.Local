using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using DryIoc;

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
    /// 用于取消后台加载任务的 CancellationTokenSource。
    /// </summary>
    private CancellationTokenSource? loadCts;

    /// <summary>
    /// 递增的重载版本号，用于避免多次 Reset 时旧加载任务回写状态。
    /// </summary>
    private long reloadVersion;

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
    /// 插件可用的图像加载策略集合（优先级按添加顺序）。
    /// </summary>
    public IEnumerable<IImageSourceStrategy> ImageStrategies { get; }

    /// <summary>
    /// 将视图平移以将指定页居中显示（仅在滚动模式下有效）。
    /// </summary>
    /// <param name="index">目标页索引（从 0 开始）。</param>
    public void ScrollToPage(int index)
    {
        if (Mode != ReadingMode.VerticalScroll)
        {
            state.CameraPos = Vector2.Zero;
            state.Velocity = Vector2.Zero;
            return;
        }

        RenderNode? targetNode;
        lock (state.LayoutNodes)
        {
            targetNode = state.LayoutNodes.FirstOrDefault(n => n.PageIndex == index);
        }

        if (targetNode != null)
        {
            var center = new Vector2((float)(targetNode.Bounds.X + targetNode.Bounds.Width / 2),
                (float)(targetNode.Bounds.Y + targetNode.Bounds.Height / 2));
            state.CameraPos = center;
            state.Velocity = Vector2.Zero;
        }
    }


    /// <summary>
    /// 创建 <see cref="MangaReader"/> 的新实例并初始化默认的图像加载策略。
    /// </summary>
    public MangaReader()
    {
        this.DefaultStyleKey = typeof(MangaReader);
        ImageStrategies = DiFactory.Services.ResolveMany<IImageSourceStrategy>();
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
            mainCanvas.SizeChanged += (_, e) =>
            {
                bool wasZero = viewSize == Vector2.Zero;
                viewSize = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
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
        var dt = (float)args.Timing.ElapsedTime.TotalSeconds;

        // 1. 物理惯性
        if (!isDragging && state.Velocity.LengthSquared() > 0.001f)
        {
            state.CameraPos += state.Velocity * dt;
            float decay = MathF.Exp(-state.Friction * dt);
            state.Velocity *= decay;
            if (state.Velocity.Length() < 1.0f) state.Velocity = Vector2.Zero;
        }

        // 2. 资源管理
        UpdateVisibleNodes(sender);

        // 3. 计算当前页
        UpdateCurrentPage();

        // 4. Update Info Panel
        UpdateInfoPanel();
    }

    /// <summary>
    /// 计算并更新当前页、缩放比例与总页数，必要时将这些信息调度到 UI 线程以更新依赖属性。
    /// </summary>
    private void UpdateCurrentPage()
    {
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
        var ds = args.DrawingSession;
        var size = viewSize;
        if (size == Vector2.Zero) return;

        var center = size / 2;
        var transform = Matrix3x2.CreateTranslation(-state.CameraPos) *
                        Matrix3x2.CreateScale(state.Zoom) *
                        Matrix3x2.CreateTranslation(center);

        ds.Transform = transform;

        // 计算视口 (用于剔除)
        Matrix3x2.Invert(transform, out var inverseTransform);
        var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
        var bottomRight = Vector2.Transform(size, inverseTransform);
        var viewportRect = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        // 绘制节点
        lock (state.LayoutNodes)
        {
            foreach (var node in state.LayoutNodes)
            {
                if (IsIntersecting(viewportRect, node.Bounds))
                {
                    if (node.IsLoaded)
                    {
                        ds.DrawImage(node.Bitmap, node.Bounds);
                    }
                    else
                    {
                        // 绘制占位符
                        ds.DrawRectangle(node.Bounds, Windows.UI.Color.FromArgb(255, 100, 100, 100));

                        // 绘制页码文字
                        using var format = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat();
                        format.FontSize = 24;
                        format.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Center;
                        format.VerticalAlignment = Microsoft.Graphics.Canvas.Text.CanvasVerticalAlignment.Center;
                        ds.DrawText($"{node.PageIndex + 1}", node.Bounds, Windows.UI.Color.FromArgb(255, 200, 200, 200),
                            format);
                    }
                }
            }
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

        Matrix3x2.Invert(transform, out var inverseTransform);
        var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
        var bottomRight = Vector2.Transform(size, inverseTransform);
        var viewportRect = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        // 扩展加载区域 (预加载前后距离)
        var loadRect = viewportRect;
        loadRect.Y -= 2000;
        loadRect.Height += 4000;

        var device = sender.Device;

        // 为了避免在 Update 线程中执行耗时操作，这里的逻辑要尽量快
        lock (state.LayoutNodes)
        {
            foreach (var node in state.LayoutNodes)
            {
                bool isVisible = IsIntersecting(loadRect, node.Bounds);

                if (isVisible)
                {
                    lock (loadingLock)
                    {
                        if (!node.IsLoaded && !loadingPages.Contains(node.PageIndex))
                        {
                            loadingPages.Add(node.PageIndex);
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    node.Bitmap = await GetBitmap(node.Ctx.Bytes, device);
                                }
                                catch
                                {
                                    // Ignore errors
                                }
                                finally
                                {
                                    lock (loadingLock)
                                    {
                                        loadingPages.Remove(node.PageIndex);
                                    }
                                }
                            });
                        }
                    }
                }
                else
                {
                    if (node.IsLoaded)
                    {
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

    // --- 输入处理 ---

    /// <summary>
    /// 指针按下处理：开始拖拽并捕获指针以实现平移交互。
    /// </summary>
    private void MainCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(mainCanvas);
        if (point.Properties.IsLeftButtonPressed)
        {
            isDragging = true;
            pointerId = (int)point.PointerId;
            lastPointerPos = point.Position.ToVector2();
            state.Velocity = Vector2.Zero;
            mainCanvas?.CapturePointer(e.Pointer);
        }
    }

    /// <summary>
    /// 指针移动处理：在拖拽期间更新摄像机位置并计算简单速度用于惯性。
    /// </summary>
    private void MainCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (isDragging && e.Pointer.PointerId == pointerId)
        {
            var point = e.GetCurrentPoint(mainCanvas);
            var currentPos = point.Position.ToVector2();
            var delta = currentPos - lastPointerPos;

            state.CameraPos -= delta / state.Zoom;

            // 简单的速度计算
            state.Velocity = -(delta / state.Zoom) / 0.016f; // 假设 60fps

            lastPointerPos = currentPos;
        }
    }

    /// <summary>
    /// 指针释放处理：结束拖拽并释放指针捕获。
    /// </summary>
    private void MainCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId == pointerId)
        {
            isDragging = false;
            pointerId = -1;
            mainCanvas?.ReleasePointerCapture(e.Pointer);
        }
    }

    /// <summary>
    /// 鼠标滚轮处理：按住 Ctrl 时进行缩放，否则在滚动模式下滚动视图，在分页模式下可用于翻页。
    /// </summary>
    private void MainCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(mainCanvas);
        var delta = point.Properties.MouseWheelDelta;
        bool isCtrlPressed = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);

        if (isCtrlPressed)
        {
            var screenPoint = point.Position.ToVector2();
            var center = viewSize / 2;

            float oldZoom = state.Zoom;
            float zoomFactor = delta > 0 ? 1.1f : 0.9f;

            float minZoom = 0.1f * baseZoomScale;
            float maxZoom = 5.0f * baseZoomScale;

            float newZoom = Math.Clamp(state.Zoom * zoomFactor, minZoom, maxZoom);
            if (Math.Abs(newZoom - oldZoom) < 0.0001f) return;

            state.Zoom = newZoom;

            // 缩放补偿：鼠标下的点在世界坐标中的位置不变
            // World = (Screen - Center) / Zoom + Camera
            // (Screen - Center) / OldZoom + OldCamera = (Screen - Center) / NewZoom + NewCamera
            // NewCamera = OldCamera + (Screen - Center) * (1/OldZoom - 1/NewZoom)

            state.CameraPos += (screenPoint - center) * (1.0f / oldZoom - 1.0f / state.Zoom);
        }
        else if (state.CurrentMode == ReadingMode.VerticalScroll)
        {
            // 停止惯性
            state.Velocity = Vector2.Zero;
            // 滚轮滚动
            state.CameraPos.Y -= delta / state.Zoom;
        }
        else if (EnableMouseWheelNavigation)
        {
            // 单页或双页模式：滚轮翻页
            if (delta != 0)
            {
                int direction = delta < 0 ? 1 : -1;
                int step = (state.CurrentMode == ReadingMode.SinglePage) ? 1 : 2;

                int target = CurrentPageIndex + (direction * step);

                if (target < 0) target = 0;
                if (target >= TotalPage) target = TotalPage > 0 ? TotalPage - 1 : 0;

                if (target != CurrentPageIndex)
                {
                    CurrentPageIndex = target;
                }
            }
        }

        e.Handled = true;
    }

    // --- 加载逻辑 ---

    /// <summary>
    /// 重新加载 ItemsSource 中的所有项，优先同步加载前 N 张以提高首次响应，其余项在后台加载。
    /// </summary>
    private async void ReloadItems()
    {
        // bump generation first so any in-flight continuations can detect staleness
        var myVersion = Interlocked.Increment(ref reloadVersion);

        // 取消之前的加载任务
        if (loadCts != null) await loadCts.CancelAsync();
        loadCts = new CancellationTokenSource();
        var token = loadCts.Token;

        // 获取绑定源
        var items = ItemsSource as System.Collections.IEnumerable;
        if (items == null) return;

        var itemList = new List<object>();
        foreach (var item in items) itemList.Add(item);

        if (itemList.Count == 0) return;

        lock (allNodes)
        {
            allNodes.Clear();
        }

        lock (state.LayoutNodes)
        {
            state.LayoutNodes.Clear();
            state.CameraPos = Vector2.Zero;
            state.Zoom = 1.0f;
            state.Velocity = Vector2.Zero;
        }

        // ensure page index resets immediately for the latest reload
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (myVersion != Volatile.Read(ref reloadVersion)) return;
            CurrentPageIndex = 0;
        });

        // 1. 优先加载前 10 张，快速响应
        int index = 0;
        float currentY = 0;
        float spacing = PageSpacing;

        var firstBatch = itemList.Take(10).ToList();
        var remainingFiles = itemList.Skip(10).ToList();

        var didEnsureFirstPage = false;

        foreach (var item in firstBatch)
        {
            if (token.IsCancellationRequested) return;
            if (myVersion != Volatile.Read(ref reloadVersion)) return;

            var node = await CreateNodeAsync(item, index++);
            if (token.IsCancellationRequested) return;
            if (myVersion != Volatile.Read(ref reloadVersion)) return;

            if (node != null)
            {
                currentY = AppendNodeToLayout(node, currentY, spacing);

                // after first successful append for the latest reload, snap to page 0
                if (!didEnsureFirstPage)
                {
                    didEnsureFirstPage = true;
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (myVersion != Volatile.Read(ref reloadVersion)) return;
                        CurrentPageIndex = 0;
                        ScrollToPage(0);
                    });
                }
            }
        }

        // 2. 后台加载剩余图片
        if (remainingFiles.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                foreach (var item in remainingFiles)
                {
                    if (token.IsCancellationRequested) return;
                    if (myVersion != Volatile.Read(ref reloadVersion)) return;

                    var node = await CreateNodeAsync(item, index++);
                    if (token.IsCancellationRequested) return;
                    if (myVersion != Volatile.Read(ref reloadVersion)) return;

                    if (node != null)
                    {
                        currentY = AppendNodeToLayout(node, currentY, spacing);
                    }
                }
            }, token);
        }
    }

    /// <summary>
    /// 使用可用策略创建并初始化一个渲染节点，返回包含尺寸信息的 <see cref="RenderNode"/>。
    /// </summary>
    /// <param name="item">原始资源标识（例如路径或 URL）。</param>
    /// <param name="index">页索引。</param>
    /// <returns>已初始化的 <see cref="RenderNode"/> 或在失败时返回 <c>null</c>。</returns>
    private async Task<RenderNode?> CreateNodeAsync(object item, int index)
    {
        try
        {
            var strategy = ImageStrategies.FirstOrDefault(s => s.CanHandle(item));
            if (strategy == null) return null;

            var ctx = new ImageLoadingContext { Source = item };
            await strategy.InitImageAsync(ctx);
            var size = ctx.Size;
            if (size.IsEmpty) size = new Size(1000, 1414); // Default size if unknown

            return new RenderNode
            {
                PageIndex = index,
                Source = item,
                Ctx = ctx,
                Bounds = new Rect(0, 0, size.Width, size.Height)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将节点追加到内部节点列表并根据当前布局模式决定是否触发布局更新。
    /// </summary>
    /// <param name="node">要追加的渲染节点。</param>
    /// <param name="currentY">当前 Y 偏移，用于顺序布局。</param>
    /// <param name="spacing">页面间距。</param>
    /// <returns>追加后下一个节点的 Y 偏移值。</returns>
    private float AppendNodeToLayout(RenderNode node, float currentY, float spacing)
    {
        lock (allNodes)
        {
            allNodes.Add(node);
        }

        // Capture necessary values
        float nodeHeight = (float)node.Bounds.Height;
        float nextY = currentY + nodeHeight + spacing;

        // Dispatch to UI thread for Mode access and LayoutNodes update
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (Mode == ReadingMode.VerticalScroll)
            {
                UpdateActiveLayout();
            }
            else
            {
                bool shouldUpdate = false;
                if (Mode == ReadingMode.SinglePage && node.PageIndex == CurrentPageIndex) shouldUpdate = true;
                else if (Mode == ReadingMode.SpreadRtl || Mode == ReadingMode.SpreadLtr)
                {
                    int spreadIndex = (CurrentPageIndex == 0) ? 0 : (CurrentPageIndex - 1) / 2 + 1;
                    if (node.PageIndex == 0 && spreadIndex == 0) shouldUpdate = true;
                    else if (node.PageIndex > 0)
                    {
                        int nodeSpreadIndex = (node.PageIndex - 1) / 2 + 1;
                        if (nodeSpreadIndex == spreadIndex) shouldUpdate = true;
                    }
                }

                if (shouldUpdate)
                {
                    UpdateActiveLayout();
                }
            }
        });

        return nextY;
    }

    /// <summary>
    /// 根据当前阅读模式和视口大小计算并更新 <see cref="EngineState.LayoutNodes"/> 中的布局信息。
    /// </summary>
    private void UpdateActiveLayout()
    {
        lock (state.LayoutNodes)
        {
            state.LayoutNodes.Clear();

            if (Mode == ReadingMode.VerticalScroll)
            {
                float currentY = 0;
                float spacing = PageSpacing;
                float scale = 1.0f;

                lock (allNodes)
                {
                    // 计算众数高度 (Mode Height)
                    if (allNodes.Count > 0 && viewSize.Y > 0)
                    {
                        var modeHeightGroup = allNodes
                            .GroupBy(n => Math.Round(n.Ctx.Size.Height)) // 简单的归组
                            .OrderByDescending(g => g.Count())
                            .FirstOrDefault();

                        if (modeHeightGroup != null)
                        {
                            double modeHeight = modeHeightGroup.Key;
                            if (modeHeight > 0)
                            {
                                scale = viewSize.Y / (float)modeHeight;
                            }
                        }
                    }

                    foreach (var node in allNodes)
                    {
                        // 应用缩放
                        double scaledWidth = node.Ctx.Size.Width * scale;
                        double scaledHeight = node.Ctx.Size.Height * scale;

                        node.Bounds.Width = scaledWidth;
                        node.Bounds.Height = scaledHeight;

                        node.Bounds.X = -scaledWidth / 2.0;
                        node.Bounds.Y = currentY;

                        currentY += (float)scaledHeight + spacing;
                        state.LayoutNodes.Add(node);
                    }
                }
            }
            else if (Mode == ReadingMode.SinglePage)
            {
                lock (allNodes)
                {
                    if (CurrentPageIndex >= 0 && CurrentPageIndex < allNodes.Count)
                    {
                        var node = allNodes[CurrentPageIndex];
                        // Center at (0,0)
                        node.Bounds.X = -node.Bounds.Width / 2.0;
                        node.Bounds.Y = -node.Bounds.Height / 2.0;
                        state.LayoutNodes.Add(node);
                    }
                }
            }
            else if (Mode == ReadingMode.SpreadRtl || Mode == ReadingMode.SpreadLtr)
            {
                lock (allNodes)
                {
                    var nodesToAdd = new List<RenderNode>();
                    if (CurrentPageIndex == 0)
                    {
                        if (allNodes.Count > 0) nodesToAdd.Add(allNodes[0]);
                    }
                    else
                    {
                        // Calculate spread start index (1-based pairs: 1-2, 3-4...)
                        // If index is 1 or 2, start is 1.
                        int pairStart = ((CurrentPageIndex - 1) / 2) * 2 + 1;

                        if (pairStart < allNodes.Count) nodesToAdd.Add(allNodes[pairStart]);
                        if (pairStart + 1 < allNodes.Count) nodesToAdd.Add(allNodes[pairStart + 1]);
                    }

                    // Layout side-by-side (RTL: Right is first/lower index, Left is second/higher index)
                    if (nodesToAdd.Count == 1)
                    {
                        var node = nodesToAdd[0];
                        node.Bounds.X = -node.Bounds.Width / 2.0;
                        node.Bounds.Y = -node.Bounds.Height / 2.0;
                        state.LayoutNodes.Add(node);
                    }
                    else if (nodesToAdd.Count == 2)
                    {
                        // Assuming RTL: Page N (Right), Page N+1 (Left)
                        // nodesToAdd[0] is First, nodesToAdd[1] is Second

                        RenderNode left, right;

                        if (Mode == ReadingMode.SpreadRtl)
                        {
                            right = nodesToAdd[0]; // First page on Right
                            left = nodesToAdd[1]; // Second page on Left
                        }
                        else // SpreadLTR
                        {
                            left = nodesToAdd[0]; // First page on Left
                            right = nodesToAdd[1]; // Second page on Right
                        }

                        float spacing = 0; // Usually spreads are tight

                        // Left Page Position (Left of center)
                        left.Bounds.X = -left.Bounds.Width - spacing / 2.0;
                        left.Bounds.Y = -left.Bounds.Height / 2.0;

                        // Right Page Position (Right of center)
                        right.Bounds.X = spacing / 2.0;
                        right.Bounds.Y = -right.Bounds.Height / 2.0;

                        state.LayoutNodes.Add(left);
                        state.LayoutNodes.Add(right);
                    }
                }
            }
        }
    }
}
