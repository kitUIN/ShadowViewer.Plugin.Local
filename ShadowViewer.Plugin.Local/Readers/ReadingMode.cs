using FluentIcons.Common;
using ShadowViewer.Controls.Attributes;

namespace ShadowViewer.Plugin.Local.Readers;

/// <summary>
/// 漫画阅读器模式
/// </summary>
public enum ReadingMode
{
    /// <summary>
    /// 纵向滚动
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.DualScreenVerticalScroll)]
    VerticalScroll,

    /// <summary>
    /// 单页模式
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.DocumentHeader)]
    SinglePage,

    /// <summary>
    /// 双页模式 (从右向左)
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.BookOpen)]
    SpreadRtl,

    /// <summary>
    /// 双页模式 (从左向右)
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.BookOpen)]
    SpreadLtr
}