using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using SqlSugar;

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
    [SugarColumn(IsPrimaryKey = true, IsNullable = false, ColumnDescription = "Id")]
    public partial long Id { get; set; }

    /// <summary>
    /// <inheritdoc cref="IAuthor.Name"/>
    /// </summary>
    [ObservableProperty]
    [SugarColumn(IsNullable = false, ColumnDescription = "作者名称")]
    public partial string Name { get; set; }
}