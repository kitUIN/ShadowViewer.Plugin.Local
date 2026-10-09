using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Constants;
using ShadowViewer.Sdk.Models.Interfaces;
using System;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 本地漫画-历史记录
/// </summary>
public partial class LocalHistory : ObservableObject, IHistory
{
    /// <summary>
    /// <inheritdoc cref="IHistory.Id"/>
    /// </summary>
    [ObservableProperty] public partial long Id { get; set; }

    /// <summary>
    /// <inheritdoc cref="IHistory.Title"/>
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = null!;

    /// <summary>
    /// <inheritdoc cref="IHistory.Thumb"/>
    /// </summary>
    [ObservableProperty] public partial string Thumb { get; set; } = null!;

    /// <summary>
    /// <inheritdoc cref="IHistory.LastReadDateTime"/>
    /// </summary>
    [ObservableProperty] public partial DateTime LastReadDateTime { get; set; }

    /// <summary>
    /// <inheritdoc cref="IHistory.Extra"/>
    /// </summary>
    [ObservableProperty] public partial string? Extra { get; set; }
    /// <summary>
    /// <inheritdoc cref="IHistory.PluginId"/>
    /// </summary>
    public virtual string PluginId { get; set; } = PluginConstants.PluginId;
}
