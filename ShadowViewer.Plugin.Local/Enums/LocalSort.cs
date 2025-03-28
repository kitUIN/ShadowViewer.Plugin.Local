using FluentIcons.Common;
using ShadowViewer.Controls.Attributes;

namespace ShadowViewer.Plugin.Local.Enums;

/// <summary>
/// 排序
/// </summary>
public enum LocalSort
{
    /// <summary>
    /// 阅读进度小-大
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.BookOpen, IconVariant = IconVariant.Filled)]
    Pa,
    /// <summary>
    /// 阅读进度大-小
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.BookOpen)]
    Pz,
    /// <summary>
    /// 字母顺序A-Z
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.TextSortAscending)]
    Az,
    /// <summary>
    /// 字母顺序Z-A
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.TextSortDescending)]
    Za,
    /// <summary>
    /// 阅读时间早-晚
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.Clock, IconVariant = IconVariant.Filled)]
    Ra,
    /// <summary>
    /// 阅读时间晚-早(默认)
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.Clock)]
    Rz,
    /// <summary>
    /// 创建时间早-晚
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.Calendar, IconVariant = IconVariant.Filled)]
    Ca,
    /// <summary>
    /// 创建时间晚-早
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.Calendar)]
    Cz
}