using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using System;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Models;

public partial class LocalReadingRecord: ObservableObject, IReadingRecord
{
    /// <summary>
    /// <inheritdoc cref="IReadingRecord.Id" />
    /// </summary>
    [ObservableProperty]
    [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    public partial long Id { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.ExtraComicId" />
    /// </summary>
    [ObservableProperty]
    [SugarColumn(IsNullable = true, ColumnDescription = "额外的漫画Id")]
    public partial string? ExtraComicId { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.Percent" />
    /// </summary>
    [ObservableProperty]
    [SugarColumn(ColumnDescription = "阅读进度")]
    public partial decimal Percent { get; set; }
    /// <summary>
    /// <inheritdoc cref="IReadingRecord.LastPicture" />
    /// </summary>
    [ObservableProperty]
    [SugarColumn(ColumnDescription = "上次阅读-页")]
    public partial int LastPicture { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.LastEpisode" />
    /// </summary>
    [ObservableProperty]
    [SugarColumn(ColumnDescription = "上次阅读-话")]
    public partial int LastEpisode { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.CreatedDateTime" />
    /// </summary>
    [ObservableProperty]
    [SugarColumn(InsertServerTime = true, ColumnDescription = "创建时间")]
    public partial DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.UpdatedDateTime" />
    /// </summary>
    [ObservableProperty]
    [SugarColumn(InsertServerTime = true, UpdateServerTime = true, ColumnDescription = "更新时间")]
    public partial DateTime UpdatedDateTime { get; set; }
}