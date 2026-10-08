using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers;

public sealed partial class MangaReader
{
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
