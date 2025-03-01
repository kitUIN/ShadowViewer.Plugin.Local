using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Core.Models.Interfaces;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Models;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
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
    [ObservableProperty] private string title;

    /// <summary>
    /// <inheritdoc cref="IHistory.Thumb"/>
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDataType = "TEXT")]
    private string thumb;

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
    [SugarColumn(IsIgnore = true)]
    public string PluginId => LocalPlugin.Meta.Id;
}
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。