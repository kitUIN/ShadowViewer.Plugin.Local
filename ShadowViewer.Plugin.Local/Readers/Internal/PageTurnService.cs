using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>
/// 卷页判定输入参数。
/// </summary>
internal readonly struct PageTurnRequest
{
    /// <summary>
    /// 初始化 <see cref="PageTurnRequest"/> 的新实例。
    /// </summary>
    /// <param name="totalDelta">从拖拽起点到释放点的总位移。</param>
    /// <param name="velocityX">释放前横向速度。</param>
    /// <param name="zoom">当前缩放值。</param>
    /// <param name="currentPageIndex">当前页索引。</param>
    /// <param name="totalPage">总页数。</param>
    /// <param name="mode">当前阅读模式。</param>
    /// <param name="layoutNodes">当前布局节点集合。</param>
    public PageTurnRequest(
        Vector2 totalDelta,
        float velocityX,
        float zoom,
        int currentPageIndex,
        int totalPage,
        ReadingMode mode,
        IReadOnlyList<RenderNode> layoutNodes)
    {
        TotalDelta = totalDelta;
        VelocityX = velocityX;
        Zoom = zoom;
        CurrentPageIndex = currentPageIndex;
        TotalPage = totalPage;
        Mode = mode;
        LayoutNodes = layoutNodes;
    }

    /// <summary>
    /// 获取从拖拽起点到释放点的总位移。
    /// </summary>
    public Vector2 TotalDelta { get; }

    /// <summary>
    /// 获取释放前横向速度。
    /// </summary>
    public float VelocityX { get; }

    /// <summary>
    /// 获取当前缩放值。
    /// </summary>
    public float Zoom { get; }

    /// <summary>
    /// 获取当前页索引。
    /// </summary>
    public int CurrentPageIndex { get; }

    /// <summary>
    /// 获取总页数。
    /// </summary>
    public int TotalPage { get; }

    /// <summary>
    /// 获取当前阅读模式。
    /// </summary>
    public ReadingMode Mode { get; }

    /// <summary>
    /// 获取当前布局节点集合。
    /// </summary>
    public IReadOnlyList<RenderNode> LayoutNodes { get; }
}

/// <summary>
/// 卷页动画计划。
/// </summary>
internal readonly struct PageTurnPlan
{
    /// <summary>
    /// 初始化 <see cref="PageTurnPlan"/> 的新实例。
    /// </summary>
    /// <param name="targetPageIndex">动画结束后目标页索引。</param>
    /// <param name="curlFromRight">是否从右侧卷起。</param>
    /// <param name="currentCurl">起始卷曲量。</param>
    /// <param name="targetCurl">目标卷曲量。</param>
    /// <param name="animVelocity">卷曲动画速度。</param>
    /// <param name="curlingNode">参与卷页的节点。</param>
    public PageTurnPlan(
        int targetPageIndex,
        bool curlFromRight,
        float currentCurl,
        float targetCurl,
        float animVelocity,
        RenderNode? curlingNode)
    {
        TargetPageIndex = targetPageIndex;
        CurlFromRight = curlFromRight;
        CurrentCurl = currentCurl;
        TargetCurl = targetCurl;
        AnimVelocity = animVelocity;
        CurlingNode = curlingNode;
    }

    /// <summary>
    /// 获取动画结束后目标页索引。
    /// </summary>
    public int TargetPageIndex { get; }

    /// <summary>
    /// 获取是否从右侧卷起。
    /// </summary>
    public bool CurlFromRight { get; }

    /// <summary>
    /// 获取起始卷曲量。
    /// </summary>
    public float CurrentCurl { get; }

    /// <summary>
    /// 获取目标卷曲量。
    /// </summary>
    public float TargetCurl { get; }

    /// <summary>
    /// 获取卷曲动画速度。
    /// </summary>
    public float AnimVelocity { get; }

    /// <summary>
    /// 获取参与卷页的节点。
    /// </summary>
    public RenderNode? CurlingNode { get; }
}

/// <summary>
/// 卷页动画单步推进结果。
/// </summary>
internal readonly struct PageTurnAnimationStepResult
{
    /// <summary>
    /// 初始化 <see cref="PageTurnAnimationStepResult"/> 的新实例。
    /// </summary>
    /// <param name="curlAmount">更新后的卷曲量。</param>
    /// <param name="velocity">更新后的动画速度。</param>
    /// <param name="isFinished">动画是否已到达目标并结束。</param>
    public PageTurnAnimationStepResult(float curlAmount, float velocity, bool isFinished)
    {
        CurlAmount = curlAmount;
        Velocity = velocity;
        IsFinished = isFinished;
    }

    /// <summary>
    /// 获取更新后的卷曲量。
    /// </summary>
    public float CurlAmount { get; }

    /// <summary>
    /// 获取更新后的动画速度。
    /// </summary>
    public float Velocity { get; }

    /// <summary>
    /// 获取动画是否结束。
    /// </summary>
    public bool IsFinished { get; }
}

/// <summary>
/// 卷页服务，负责根据拖拽手势生成翻页动画计划。
/// </summary>
internal sealed class PageTurnService
{
    /// <summary>
    /// 触发滑动判定的最小位移阈值（像素）。
    /// </summary>
    private const float SwipeDistanceThreshold = 50f;

    /// <summary>
    /// 触发滑动判定的最小速度阈值（像素/秒）。
    /// </summary>
    private const float SwipeVelocityThreshold = 500f;

    /// <summary>
    /// 触发卷页动画的最小水平拖拽阈值（像素）。
    /// </summary>
    private const float MinCurlDistanceThreshold = 10f;

    /// <summary>
    /// 卷页动画最短时长（秒），用于估算速度下限。
    /// </summary>
    private const float TargetDurationSeconds = 0.3f;

    /// <summary>
    /// 卷页动画最小速度（像素/秒）。
    /// </summary>
    private const float MinCurlVelocity = 1500f;

    /// <summary>
    /// 全卷曲目标倍率。
    /// </summary>
    private const float FullCurlScale = 1.5f;

    /// <summary>
    /// 尝试根据当前手势创建卷页动画计划。
    /// </summary>
    /// <param name="request">卷页判定输入参数。</param>
    /// <param name="plan">输出的卷页动画计划。</param>
    /// <returns>若成功生成动画计划返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public bool TryCreatePlan(PageTurnRequest request, out PageTurnPlan plan)
    {
        float absX = Math.Abs(request.TotalDelta.X);
        if (absX <= MinCurlDistanceThreshold || request.Zoom <= 0)
        {
            plan = default;
            return false;
        }

        // 仅当水平意图明显时允许翻页，避免垂直拖动误触发分页跳转。
        bool isSwipe = absX > SwipeDistanceThreshold || Math.Abs(request.VelocityX * request.Zoom) > SwipeVelocityThreshold;

        int targetIndex = request.CurrentPageIndex;
        if (isSwipe && absX > Math.Abs(request.TotalDelta.Y))
        {
            int direction = request.TotalDelta.X > 0 ? -1 : 1;
            int step = request.Mode == ReadingMode.SinglePage ? 1 : 2;
            targetIndex = ClampPageIndex(request.CurrentPageIndex + direction * step, request.TotalPage);
        }

        bool curlFromRight = request.TotalDelta.X < 0;
        float currentCurl = absX / request.Zoom;

        RenderNode? curlingNode = curlFromRight
            ? request.LayoutNodes.OrderByDescending(n => n.Bounds.X).FirstOrDefault()
            : request.LayoutNodes.OrderBy(n => n.Bounds.X).FirstOrDefault();

        float targetCurl = 0f;
        if (targetIndex != request.CurrentPageIndex && curlingNode != null)
        {
            // 只有确实发生翻页时才卷到“完全翻过”，否则回卷到 0，视觉上更可预期。
            targetCurl = (float)curlingNode.Bounds.Width * FullCurlScale;
        }

        float distance = Math.Abs(targetCurl - currentCurl);
        float velocity = Math.Max(MinCurlVelocity, distance / TargetDurationSeconds);
        if (targetCurl < currentCurl)
        {
            velocity = -velocity;
        }

        plan = new PageTurnPlan(targetIndex, curlFromRight, currentCurl, targetCurl, velocity, curlingNode);
        return true;
    }

    /// <summary>
    /// 推进卷页动画一帧并返回收敛结果。
    /// </summary>
    /// <param name="currentCurl">当前卷曲量。</param>
    /// <param name="targetCurl">目标卷曲量。</param>
    /// <param name="velocity">当前动画速度。</param>
    /// <param name="deltaTime">帧间隔（秒）。</param>
    /// <returns>包含新卷曲量、新速度和结束标记的结果。</returns>
    public PageTurnAnimationStepResult StepAnimation(float currentCurl, float targetCurl, float velocity, float deltaTime)
    {
        if (velocity == 0 || deltaTime <= 0)
        {
            return new PageTurnAnimationStepResult(currentCurl, velocity, false);
        }

        float nextCurl = currentCurl + velocity * deltaTime;

        // 通过按速度方向比较阈值，确保不同方向卷曲都能在同一逻辑下稳定收敛到目标。
        bool isFinished = false;
        if (velocity > 0 && nextCurl >= targetCurl)
        {
            nextCurl = targetCurl;
            isFinished = true;
        }
        else if (velocity < 0 && nextCurl <= targetCurl)
        {
            nextCurl = targetCurl;
            isFinished = true;
        }

        float nextVelocity = isFinished ? 0f : velocity;
        return new PageTurnAnimationStepResult(nextCurl, nextVelocity, isFinished);
    }

    /// <summary>
    /// 将页码索引限制在有效范围内。
    /// </summary>
    /// <param name="index">待限制的页码索引。</param>
    /// <param name="totalPage">总页数。</param>
    /// <returns>限制后的有效页码索引。</returns>
    private static int ClampPageIndex(int index, int totalPage)
    {
        if (index < 0)
        {
            return 0;
        }

        int maxIndex = totalPage > 0 ? totalPage - 1 : 0;
        if (index > maxIndex)
        {
            return maxIndex;
        }

        return index;
    }
}