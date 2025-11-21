using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Constants;
using ShadowViewer.Sdk.Models.Interfaces;
using SqlSugar;
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
    [ObservableProperty] [property: SugarColumn(IsPrimaryKey = true)]
    private long id;

    /// <summary>
    /// <inheritdoc cref="IHistory.Title"/>
    /// </summary>
    [ObservableProperty] private string title = null!;

    /// <summary>
    /// <inheritdoc cref="IHistory.Thumb"/>
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDataType = "TEXT")]
    private string thumb = null!;

    /// <summary>
    /// <inheritdoc cref="IHistory.LastReadDateTime"/>
    /// </summary>
    [ObservableProperty] private DateTime lastReadDateTime;

    /// <summary>
    /// <inheritdoc cref="IHistory.Extra"/>
    /// </summary>
    [ObservableProperty] [property: SugarColumn(IsNullable = true)]
    private string? extra;

    /// <summary>
    /// <inheritdoc cref="IHistory.PluginId"/>
    /// </summary>
    public virtual string PluginId { get; set; } = PluginConstants.PluginId;
}
