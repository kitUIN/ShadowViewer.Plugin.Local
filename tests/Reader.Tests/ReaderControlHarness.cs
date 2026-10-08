// Runs the production page-turn lifecycle with a synchronous property adapter.
// WinUI bindings, dispatcher scheduling and native input are not simulated.
using System.Numerics;
using ShadowViewer.Plugin.Local.Readers.Internal;

namespace ShadowViewer.Plugin.Local.Readers;

public sealed partial class MangaReader
{
    private readonly EngineState state;
    private readonly List<RenderNode> nodes;
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
    public int TargetIndex => pageTurnTargetIndex;
    public void FinishAnimation() => CompletePageTurn(pageTurnTargetIndex);
    public void InterruptAnimation() => InterruptPageTurn();
    internal void StartGesture(PageTurnPlan plan) => StartPageTurn(plan);
    private void UpdateActiveLayout() => new ReaderLayoutService().UpdateActiveLayout(state, nodes,
        CurrentPageIndex, 0, false, new Vector2(2000, 1400), new ReaderLayoutCacheState());
    private void ResetZoom()
    {
        CancelPageTurn(resetInput: true);
        state.Zoom = baseZoomScale;
    }
}
