using System.Numerics;
using ShadowViewer.Plugin.Local.Readers.Internal;

namespace ShadowViewer.Plugin.Local.Readers;

public sealed partial class MangaReader
{
    private bool isCompletingPageTurn;
    private int pageTurnSourceIndex;

    /// <summary>
    /// 保留旧双页直到自动动画完成；连续请求会取消动画并直接定位最新目标。
    /// </summary>
    private void HandleCurrentPageIndexChanged(int currentIndex, int targetIndex)
    {
        if (isCompletingPageTurn) return;
        bool wasAnimating = isAnimatingPageTurn;
        CancelPageTurn(resetInput: true);
        if (!wasAnimating)
        {
            PageTurnPlan plan;
            bool hasPlan;
            lock (state.LayoutNodes)
            {
                hasPlan = pageTurnService.TryCreateAutomaticPlan(currentIndex, targetIndex, TotalPage,
                    state.CurrentMode, state.Zoom, baseZoomScale, state.LayoutNodes, out plan);
            }
            if (hasPlan)
            {
                StartPageTurn(plan, currentIndex);
                return;
            }
        }
        UpdateActiveLayout();
        ResetZoom();
    }

    /// <summary>
    /// 使用同一入口启动手势和自动翻页，清除相机惯性。
    /// </summary>
    private void StartPageTurn(PageTurnPlan plan, int? sourceIndex = null)
    {
        lock (pageTurnLock)
        {
            pageTurnVersion++;
            isAnimatingPageTurn = true;
            pageTurnSourceIndex = sourceIndex ?? CurrentPageIndex;
            pageTurnTargetIndex = plan.TargetPageIndex;
            pageTurnCurlFromRight = plan.CurlFromRight;
            pageTurnAnimCurlAmount = plan.CurrentCurl;
            pageTurnCurlingNode = plan.CurlingNode;
            pageTurnAnimTargetCurl = plan.TargetCurl;
            pageTurnAnimVelocity = plan.AnimVelocity;
            state.Velocity = Vector2.Zero;
            state.ZoomVelocity = 0;
        }
    }

    /// <summary>
    /// 指针接管自动卷页时恢复起始页码，使后续手势与仍可见的布局一致。
    /// </summary>
    private void InterruptPageTurn()
    {
        lock (pageTurnLock)
        {
            if (isAnimatingPageTurn && CurrentPageIndex != pageTurnSourceIndex)
            {
                isCompletingPageTurn = true;
                try { CurrentPageIndex = pageTurnSourceIndex; }
                finally { isCompletingPageTurn = false; }
            }
            CancelPageTurn();
        }
    }

    /// <summary>
    /// 动画完成后应用目标布局，避免依赖属性回调再次播放同一动画。
    /// </summary>
    private void CompletePageTurn(int targetIndex)
    {
        CancelPageTurn();
        isCompletingPageTurn = true;
        try
        {
            if (targetIndex != CurrentPageIndex) CurrentPageIndex = targetIndex;
            UpdateActiveLayout();
            ResetZoom();
        }
        finally
        {
            isCompletingPageTurn = false;
        }
    }

    /// <summary>
    /// 取消当前卷页并使已排队的完成回调失效。
    /// </summary>
    private void CancelPageTurn(bool resetInput = false)
    {
        lock (pageTurnLock)
        {
            pageTurnVersion++;
            isAnimatingPageTurn = false;
            pageTurnTargetIndex = -1;
            pageTurnAnimCurlAmount = 0;
            pageTurnAnimTargetCurl = 0;
            pageTurnAnimVelocity = 0;
            pageTurnCurlingNode = null;
            lastReportedPage = -1;
        }

        if (resetInput)
        {
            inputController.Reset();
            isDragging = false;
            isUserInteracting = false;
            state.Velocity = Vector2.Zero;
            state.ZoomVelocity = 0;
        }
    }
}
