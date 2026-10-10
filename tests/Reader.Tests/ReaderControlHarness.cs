// Runs the production page-turn lifecycle with a synchronous property adapter.
// WinUI bindings, dispatcher scheduling and native input are not simulated.
using System.Numerics;
using Microsoft.Graphics.Canvas;
using ShadowViewer.Plugin.Local.Readers.Internal;
using ShadowViewer.Plugin.Local.Readers.Rendering;

namespace ShadowViewer.Plugin.Local.Readers;

public sealed partial class MangaReader
{
    private readonly EngineState state;
    private readonly List<RenderNode> nodes;
    private List<RenderNode> allNodes => nodes;
    private readonly IPageRenderer pageRenderer = new Win2DPageRenderer();
    private readonly ReaderImageWindowLoadController imageWindowLoadController = new();
    private readonly Vector2 viewSize = new(2000, 1400);
    private readonly Vector2 lastPointerPos = Vector2.Zero;
    private readonly int preloadRange = 0;
    private readonly PageTurnService pageTurnService = new();
    private readonly ReaderInputController inputController = new();
    private readonly object pageTurnLock = new();
    private int pageTurnVersion, pageTurnTargetIndex = -1, lastReportedPage = -1;
    private bool isAnimatingPageTurn, isDragging, isUserInteracting, pageTurnCurlFromRight;
    private float pageTurnAnimCurlAmount, pageTurnAnimTargetCurl, pageTurnAnimVelocity, baseZoomScale = 1;
    private RenderNode? pageTurnCurlingNode;
    private int currentPageIndex;

    public MangaReader(EngineState state, List<RenderNode> nodes, int index)
    {
        this.state = state; this.nodes = nodes; currentPageIndex = index;
    }

    public int TotalPage => nodes.Count;
    public int CurrentPageIndex
    {
        get => currentPageIndex;
        set
        {
            if (value == currentPageIndex) return;
            int old = currentPageIndex;
            currentPageIndex = value;
            HandleCurrentPageIndexChanged(old, value);
        }
    }
    public bool AnimationActive => isAnimatingPageTurn;
    public int LayoutUpdates { get; private set; }
    public int TargetIndex => pageTurnTargetIndex;
    public void FinishAnimation()
    {
        // The production dispatcher holds this lock for the whole completion callback.
        lock (pageTurnLock) CompletePageTurn(pageTurnTargetIndex);
    }
    public object FrameGate => pageTurnLock;
    public void Draw(CanvasDrawingSession drawing) => DrawFrame(drawing);
    public void RefreshImages() => UpdateImageWindow(new CanvasDevice());
    private bool TryEnqueueBitmapLoad(ReaderImageLoadTicket ticket) => false;
    public void InterruptAnimation() => InterruptPageTurn();
    internal void StartGesture(PageTurnPlan plan) => StartPageTurn(plan);
    private void UpdateActiveLayout()
    {
        LayoutUpdates++;
        new ReaderLayoutService().UpdateActiveLayout(state, nodes,
            CurrentPageIndex, 0, false, new Vector2(2000, 1400), new ReaderLayoutCacheState());
    }
    private void ResetZoom()
    {
        CancelPageTurn(resetInput: true);
        state.Zoom = baseZoomScale;
    }
}
