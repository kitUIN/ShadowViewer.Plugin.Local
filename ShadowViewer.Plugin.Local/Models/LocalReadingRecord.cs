using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using System;

namespace ShadowViewer.Plugin.Local.Models;

public partial class LocalReadingRecord: ObservableObject, IReadingRecord
{
    /// <summary>
    /// <inheritdoc cref="IReadingRecord.Id" />
    /// </summary>
    [ObservableProperty]
    public partial long Id { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.ExtraComicId" />
    /// </summary>
    [ObservableProperty]
    public partial string? ExtraComicId { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.Percent" />
    /// </summary>
    [ObservableProperty]
    public partial decimal Percent { get; set; }
    /// <summary>
    /// <inheritdoc cref="IReadingRecord.LastPicture" />
    /// </summary>
    [ObservableProperty]
    public partial int LastPicture { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.LastEpisode" />
    /// </summary>
    [ObservableProperty]
    public partial int LastEpisode { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.CreatedDateTime" />
    /// </summary>
    [ObservableProperty]
    public partial DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// <inheritdoc cref="IReadingRecord.UpdatedDateTime" />
    /// </summary>
    [ObservableProperty]
    public partial DateTime UpdatedDateTime { get; set; }
}
