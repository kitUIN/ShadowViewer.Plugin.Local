using System;
using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>
/// 帧状态编排器，负责在单帧中推进输入合成、相机状态与惯性物理。
/// </summary>
internal sealed class ReaderFrameOrchestrator
{
    /// <summary>
    /// 推进单帧状态并返回卷页动画推进结果。
    /// </summary>
    /// <param name="state">阅读器运行状态。</param>
    /// <param name="inputDelta">当前帧输入增量。</param>
    /// <param name="deltaTime">帧间隔（秒）。</param>
    /// <param name="baseZoomScale">基准缩放比例。</param>
    /// <param name="viewSize">当前视口尺寸。</param>
    /// <param name="isDragging">是否正在拖拽。</param>
    /// <param name="allowHorizontalDragInScrollMode">滚动模式是否允许水平拖拽。</param>
    /// <param name="isAnimatingPageTurn">当前是否处于卷页动画。</param>
    /// <param name="pageTurnAnimCurlAmount">当前卷曲量。</param>
    /// <param name="pageTurnAnimTargetCurl">目标卷曲量。</param>
    /// <param name="pageTurnAnimVelocity">卷曲速度。</param>
    /// <param name="lastZoomCenter">最后有效缩放中心。</param>
    /// <param name="pageTurnService">卷页动画服务。</param>
    /// <returns>包含本帧卷页推进和缩放中心更新结果。</returns>
    public ReaderFrameStepResult Step(
        EngineState state,
        in InputFrameDelta inputDelta,
        float deltaTime,
        float baseZoomScale,
        Vector2 viewSize,
        bool isDragging,
        bool allowHorizontalDragInScrollMode,
        bool isAnimatingPageTurn,
        float pageTurnAnimCurlAmount,
        float pageTurnAnimTargetCurl,
        float pageTurnAnimVelocity,
        Vector2 lastZoomCenter,
        PageTurnService pageTurnService)
    {
        Vector2 deltaToApply = inputDelta.PanDelta;
        float zoomToApply = inputDelta.ZoomDelta;
        Vector2 zoomCenter = inputDelta.ZoomCenter;

        if (Math.Abs(zoomToApply - 1.0f) > 0.0001f)
        {
            float oldZoom = state.Zoom;
            float minZoom = 0.1f * baseZoomScale;
            float maxZoom = 5.0f * baseZoomScale;
            state.Zoom = Math.Clamp(state.Zoom * zoomToApply, minZoom, maxZoom);

            state.CameraPos += (zoomCenter - viewSize / 2f) * (1.0f / oldZoom - 1.0f / state.Zoom);

            if (isDragging && deltaTime > 0)
            {
                state.ZoomVelocity = (zoomToApply - 1.0f) / deltaTime;
                lastZoomCenter = zoomCenter;
            }
        }

        if (deltaToApply != Vector2.Zero)
        {
            bool isZoomed = Math.Abs(state.Zoom - baseZoomScale) > 0.001f;
            bool isSpreadMode = state.CurrentMode == ReadingMode.SpreadLtr || state.CurrentMode == ReadingMode.SpreadRtl;
            bool canDrag = !isSpreadMode || isZoomed;

            if (canDrag)
            {
                if (state.CurrentMode == ReadingMode.VerticalScroll && !allowHorizontalDragInScrollMode && !isZoomed)
                {
                    deltaToApply.X = 0;
                    state.Velocity = Vector2.Zero;
                }
                else if (isDragging && deltaTime > 0)
                {
                    state.Velocity = -deltaToApply / state.Zoom / deltaTime;
                }

                state.CameraPos -= deltaToApply / state.Zoom;
            }
            else
            {
                state.Velocity = Vector2.Zero;
            }
        }

        bool pageTurnFinished = false;

        if (!isDragging)
        {
            if (isAnimatingPageTurn)
            {
                if (pageTurnAnimVelocity != 0)
                {
                    var stepResult = pageTurnService.StepAnimation(
                        pageTurnAnimCurlAmount,
                        pageTurnAnimTargetCurl,
                        pageTurnAnimVelocity,
                        deltaTime);

                    pageTurnAnimCurlAmount = stepResult.CurlAmount;
                    pageTurnAnimVelocity = stepResult.Velocity;
                    pageTurnFinished = stepResult.IsFinished;
                }
            }
            else
            {
                if (state.Velocity.LengthSquared() > 0.001f)
                {
                    state.CameraPos += state.Velocity * deltaTime;
                    float decay = MathF.Exp(-state.Friction * deltaTime);
                    state.Velocity *= decay;
                    if (state.Velocity.Length() < 1.0f)
                    {
                        state.Velocity = Vector2.Zero;
                    }
                }

                if (Math.Abs(state.ZoomVelocity) > 0.001f)
                {
                    float oldZoom = state.Zoom;
                    float zoomStep = 1.0f + state.ZoomVelocity * deltaTime;

                    float minZoom = 0.1f * baseZoomScale;
                    float maxZoom = 5.0f * baseZoomScale;
                    state.Zoom = Math.Clamp(state.Zoom * zoomStep, minZoom, maxZoom);

                    state.CameraPos += (lastZoomCenter - viewSize / 2f) * (1.0f / oldZoom - 1.0f / state.Zoom);

                    float decay = MathF.Exp(-state.Friction * deltaTime);
                    state.ZoomVelocity *= decay;
                    if (Math.Abs(state.ZoomVelocity) < 0.01f)
                    {
                        state.ZoomVelocity = 0;
                    }
                }
            }
        }

        return new ReaderFrameStepResult(
            pageTurnAnimCurlAmount,
            pageTurnAnimVelocity,
            lastZoomCenter,
            pageTurnFinished);
    }
}

/// <summary>
/// 帧推进结果。
/// </summary>
/// <param name="PageTurnAnimCurlAmount">更新后的卷曲量。</param>
/// <param name="PageTurnAnimVelocity">更新后的卷曲速度。</param>
/// <param name="LastZoomCenter">更新后的缩放中心缓存。</param>
/// <param name="PageTurnFinished">卷页动画本帧是否到达终点。</param>
internal readonly record struct ReaderFrameStepResult(
    float PageTurnAnimCurlAmount,
    float PageTurnAnimVelocity,
    Vector2 LastZoomCenter,
    bool PageTurnFinished);
