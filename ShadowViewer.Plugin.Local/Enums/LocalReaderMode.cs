using ShadowViewer.Controls.Attributes;
using FluentIcons.Common;

namespace ShadowViewer.Plugin.Local.Enums;

/// <summary>
/// 阅读模式
/// </summary>
public enum LocalReaderMode
{
    /// <summary>
    /// 单页模式
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.DocumentHeader)]
    SinglePage = 0,

    /// <summary>
    /// 双页模式
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.BookOpen)]
    DoublePage,

    /// <summary>
    /// 竖直滚动模式
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.DualScreenVerticalScroll)]
    VerticalScrolling,
    
    // /// <summary>
    // /// 横向滚动模式
    // /// </summary>
    // [MenuFlyoutItemIcon(Icon = Icon.DualScreenVerticalScroll)]
    // HorizontalScrolling,
}