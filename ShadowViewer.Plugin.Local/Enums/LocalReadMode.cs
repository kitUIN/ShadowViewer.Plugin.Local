using ShadowViewer.Controls.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentIcons.Common;

namespace ShadowViewer.Plugin.Local.Enums;

/// <summary>
/// 阅读模式
/// </summary>
public enum LocalReadMode
{
    /// <summary>
    /// 双页模式
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.BookOpen)]
    TwoPage,

    /// <summary>
    /// 滚动模式
    /// </summary>
    [MenuFlyoutItemIcon(Icon = Icon.DualScreenVerticalScroll)]
    Scrolling,
}