using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using System;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Models
{
    public partial class LocalReadingRecord: ObservableObject, IReadingRecord
    {
        /// <summary>
        /// <inheritdoc cref="IReadingRecord.Id" />
        /// </summary>
        [ObservableProperty]
        [property: SugarColumn(IsPrimaryKey = true, IsNullable = false)]
        private long id;

        /// <summary>
        /// <inheritdoc cref="IReadingRecord.ExtraComicId" />
        /// </summary>
        [ObservableProperty]
        [property: SugarColumn(IsNullable = true, ColumnDescription = "额外的漫画Id")]
        private string? extraComicId;

        /// <summary>
        /// <inheritdoc cref="IReadingRecord.Percent" />
        /// </summary>
        [ObservableProperty]
        [property: SugarColumn(ColumnDescription = "阅读进度")]
        private decimal percent;
        /// <summary>
        /// <inheritdoc cref="IReadingRecord.LastPicture" />
        /// </summary>
        [ObservableProperty]
        [property: SugarColumn(ColumnDescription = "上次阅读-页")]
        private int lastPicture;

        /// <summary>
        /// <inheritdoc cref="IReadingRecord.LastEpisode" />
        /// </summary>
        [ObservableProperty]
        [property: SugarColumn(ColumnDescription = "上次阅读-话")]
        private int lastEpisode;

        /// <summary>
        /// <inheritdoc cref="IReadingRecord.CreatedDateTime" />
        /// </summary>
        [ObservableProperty]
        [property: SugarColumn(InsertServerTime = true, ColumnDescription = "创建时间")]
        private DateTime createdDateTime;
        /// <summary>
        /// <inheritdoc cref="IReadingRecord.UpdatedDateTime" />
        /// </summary>
        [ObservableProperty]
        [property: SugarColumn(InsertServerTime = true, UpdateServerTime = true, ColumnDescription = "更新时间")]
        private DateTime updatedDateTime;
    }
}
