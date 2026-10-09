using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 本地作者
/// </summary>
public partial class LocalAuthor : ObservableObject, IAuthor
{
    /// <summary>
    /// <inheritdoc cref="IAuthor.Id"/>
    /// </summary>
    [ObservableProperty]
    public partial long Id { get; set; }

    /// <summary>
    /// <inheritdoc cref="IAuthor.Name"/>
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; }
}
