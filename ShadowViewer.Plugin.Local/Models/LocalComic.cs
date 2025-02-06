using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DryIoc;
using Serilog;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Models;
using ShadowViewer.Models.Interfaces;
using SqlSugar;
namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 本地漫画
/// </summary>
public partial class LocalComic : ObservableObject, IComic
{
    public const string DefaultFolderImg = "ms-appx:///Assets/Default/folder.png";
    #region Private Field

    /// <summary>
    /// <inheritdoc cref="IComic.Id" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(IsPrimaryKey = true, IsNullable = false)]
    private long id;
    /// <summary>
    /// <inheritdoc cref="IComic.ParentId" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn( ColumnDescription = "父Id")]
    private long parentId;
    /// <summary>
    /// <inheritdoc cref="IComic.ComicId" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDescription = "漫画Id")]
    private string? comicId;
    /// <summary>
    /// <inheritdoc cref="IComic.Name" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDataType = "Nvarchar(255)", IsNullable = false, ColumnDescription = "漫画名称")]
    private string name;

    /// <summary>
    /// <inheritdoc cref="IComic.Thumb" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "漫画缩略图")]
    private string? thumb;

    /// <summary>
    /// <inheritdoc cref="IComic.Remark" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "漫画备注")]
    private string? remark;
    /// <summary>
    /// <inheritdoc cref="IComic.Link" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "路径链接")]
    private string? link;
    /// <summary>
    /// <inheritdoc cref="IComic.CreatedDateTime" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(InsertServerTime = true, ColumnDescription = "创建时间")]
    private DateTime createdDateTime;
    /// <summary>
    /// <inheritdoc cref="IComic.UpdatedDateTime" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(UpdateServerTime = true, ColumnDescription = "更新时间")]
    private DateTime updatedDateTime;

    /// <summary>
    /// <inheritdoc cref="IComic.Affiliation" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDescription = "分类", IsNullable = false)]
    private string affiliation;


    /// <summary>
    /// <inheritdoc cref="IComic.Size" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDescription = "存储空间")]
    private long size;
    /// <summary>
    /// <inheritdoc cref="IComic.EpisodeCount" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDescription = "话-数量")]
    private int episodeCount;
    /// <summary>
    /// <inheritdoc cref="IComic.Count" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDescription = "页-数量")]
    private int count;
    /// <summary>
    /// <inheritdoc cref="IComic.IsFolder" />
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDescription = "是否是文件夹")]
    private bool isFolder;


    #endregion


    [Navigate(typeof(ComicAuthor), nameof(ComicAuthor.ComicId), nameof(ComicAuthor.AuthorId), nameof(LocalComic.Id), nameof(LocalAuthor.Id))]
    public List<IAuthor> Authors { get; set; }
    /// <summary>
    /// 标签
    /// </summary>
    [Navigate(NavigateType.OneToMany, nameof(LocalTag.ComicId), nameof(Id))]
    public List<LocalTag> Tags { get; set; }

    /// <summary>
    /// Logger
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    private static ILogger Logger { get; } = Log.ForContext<LocalComic>();
}
