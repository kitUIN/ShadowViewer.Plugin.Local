using System;
using System.Numerics;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>统一分页、手势与章节边界判定；定位页码本身不会请求切换章节。</summary>
internal static class ReaderNavigationService
{
    public static bool IsPageBoundary(int index, int count, ReadingMode mode, int direction)
    {
        if (count <= 0 || index < 0 || index >= count || direction == 0) return false;
        if (direction < 0) return index == 0;
        int lastVisible = mode is ReadingMode.SpreadLtr or ReadingMode.SpreadRtl && index > 0
            ? (index - 1) / 2 * 2 + 2 : index;
        return lastVisible >= count - 1;
    }

    public static int GetTargetPageIndex(int index, int count, ReadingMode mode, int direction)
    {
        if (count <= 0 || direction == 0 || IsPageBoundary(index, count, mode, direction)) return index;
        int step = mode is ReadingMode.SpreadLtr or ReadingMode.SpreadRtl ? 2 : 1;
        return Math.Clamp(index + Math.Sign(direction) * step, 0, count - 1);
    }

    public static int GetSwipeDirection(Vector2 delta, float velocityX, ReadingMode mode, bool canceled)
    {
        float distance = Math.Abs(delta.X);
        if (canceled || distance <= 10 || distance <= Math.Abs(delta.Y) ||
            (distance <= 50 && Math.Abs(velocityX) <= 500)) return 0;
        return PageTurnService.GetPageDirection(delta.X < 0, mode);
    }

    public static bool IsScrollBoundary(EngineState state, Vector2 viewSize, int direction)
    {
        if (direction == 0 || state.Zoom <= 0 || viewSize.Y <= 0) return false;
        lock (state.LayoutNodes)
        {
            if (state.LayoutNodes.Count == 0) return false;
            var first = state.LayoutNodes[0].Bounds;
            var last = state.LayoutNodes[^1].Bounds;
            double center = (first.Y + last.Y + last.Height) / 2;
            double halfViewport = viewSize.Y / state.Zoom / 2;
            // 短章节以内容中心为边界，长章节需真正读到首尾，不能仅凭当前页码。
            return direction < 0
                ? state.CameraPos.Y <= Math.Min(first.Y + halfViewport, center) + 1
                : state.CameraPos.Y >= Math.Max(last.Y + last.Height - halfViewport, center) - 1;
        }
    }

    public static int GetScrollSwipeDirection(Vector2 delta, bool startedAtTop, bool startedAtBottom, bool canceled)
    {
        if (canceled || Math.Abs(delta.Y) <= 50 || Math.Abs(delta.Y) <= Math.Abs(delta.X)) return 0;
        if (delta.Y < 0 && startedAtBottom) return 1;
        if (delta.Y > 0 && startedAtTop) return -1;
        return 0;
    }
}
