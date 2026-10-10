using System;
using System.Numerics;
using ShadowViewer.Plugin.Local.Readers.Internal;

namespace ShadowViewer.Plugin.Local.Readers;

public sealed partial class MangaReader
{
    /// <summary>继续越过章节首尾时触发，负数为上一话，正数为下一话。</summary>
    public event EventHandler<int>? ChapterNavigationRequested;

    /// <summary>点击、命令和滚轮共用的翻页入口。</summary>
    public void TurnPage(int direction)
    {
        if (TotalPage <= 0 || direction == 0) return;
        if (ReaderNavigationService.IsPageBoundary(CurrentPageIndex, TotalPage, state.CurrentMode, direction))
        {
            // 双页自动动画尚未结束时，当前页码可能已经是目标页码。
            if (!isAnimatingPageTurn) RequestChapterNavigation(direction);
            return;
        }

        CurrentPageIndex = ReaderNavigationService.GetTargetPageIndex(
            CurrentPageIndex, TotalPage, state.CurrentMode, direction);
    }

    private void RequestChapterNavigation(int direction)
    {
        state.Velocity = Vector2.Zero;
        ChapterNavigationRequested?.Invoke(this, Math.Sign(direction));
    }
}
