using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DryIoc;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Sdk.Models;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 本地漫画
/// </summary>
[SugarIndex("index_local_comic_affiliation", nameof(Affiliation), OrderByType.Asc)]
[SugarIndex("index_local_comic_createdDateTime", nameof(CreatedDateTime), OrderByType.Asc)]
[SugarIndex("index_local_comic_updatedDateTime", nameof(UpdatedDateTime), OrderByType.Asc)]
[SugarIndex("index_local_comic_name", nameof(Name), OrderByType.Asc)]
[SugarIndex("index_local_comic_is_delete", nameof(IsDelete), OrderByType.Asc)]
public partial class LocalComic : ObservableObject
{
    #region Field

    /// <summary>
    /// Id
    /// </summary>
    [ObservableProperty] [property: SugarColumn(IsPrimaryKey = true)]
    private long id;

    /// <summary>
    /// 父Id
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "父Id")]
    private long parentId;

    /// <summary>
    /// 漫画Id
    /// </summary>
    [ObservableProperty] [property: SugarColumn(IsNullable = true, ColumnDescription = "漫画Id")]
    private string? comicId;

    /// <summary>
    /// 漫画名称
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDataType = "Nvarchar(255)", ColumnDescription = "漫画名称", IsNullable = false)]
    private string name = null!;

    /// <summary>
    /// 漫画缩略图
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(ColumnDataType = "TEXT", DefaultValue = "mx-appx:///default.png",
        ColumnDescription = "漫画缩略图")]
    private string thumb = "mx-appx:///default.png";

    /// <summary>
    /// 漫画备注
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "漫画备注")]
    private string? remark;

    /// <summary>
    /// 路径链接
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDataType = "TEXT", IsNullable = true, ColumnDescription = "路径链接")]
    private string? link;

    /// <summary>
    /// 创建时间
    /// </summary>
    [ObservableProperty] [property: SugarColumn(InsertServerTime = true, ColumnDescription = "创建时间")]
    private DateTime createdDateTime;

    /// <summary>
    /// 更新时间
    /// </summary>
    [ObservableProperty]
    [property: SugarColumn(InsertServerTime = true, UpdateServerTime = true, ColumnDescription = "更新时间")]
    private DateTime updatedDateTime;

    /// <summary>
    /// 分类
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "分类", IsNullable = false)]
    private string affiliation = null!;


    /// <summary>
    /// 存储空间
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "存储空间")]
    private long size;

    /// <summary>
    /// 话-数量
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "话-数量")]
    private int episodeCount;

    /// <summary>
    /// 页-数量
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "页-数量")]
    private int count;

    /// <summary>
    /// 是否是文件夹
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "是否是文件夹")]
    private bool isFolder;

    /// <summary>
    /// 是否删除
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "是否删除")]
    private bool isDelete;

    /// <summary>
    /// 是否损坏
    /// </summary>
    [ObservableProperty] [property: SugarColumn(ColumnDescription = "是否损坏")]
    private bool isBroken;

    /// <summary>
    /// 作者
    /// </summary>
    [Navigate(typeof(LocalComicAuthorMapping), nameof(LocalComicAuthorMapping.ComicId),
        nameof(LocalComicAuthorMapping.AuthorId))]
    public List<LocalAuthor> Authors { get; set; } = null!;

    /// <summary>
    /// 标签
    /// </summary>
    [Navigate(typeof(LocalComicTagMapping), nameof(LocalComicTagMapping.ComicId),
        nameof(LocalComicTagMapping.TagId))]
    public List<ShadowTag> Tags { get; set; } = null!;


    /// <summary>
    /// 阅读记录
    /// </summary>
    [Navigate(NavigateType.OneToOne, nameof(Id))]
    public LocalReadingRecord ReadingRecord { get; set; } = null!;

    #endregion

    /// <summary>
    /// 新建文件夹
    /// </summary>
    /// <param name="name">文件夹名称</param>
    /// <param name="parentId">父级Id</param>
    /// <param name="id"></param>
    public static void CreateFolder(string? name, long parentId = -1, long? id = null)
    {
        if (string.IsNullOrEmpty(name)) name = I18N.NewFolder;
        var i = 1;
        var db = DiFactory.Services.Resolve<ISqlSugarClient>();
        // ReSharper disable once AccessToModifiedClosure
        while (db.Queryable<LocalComic>().Any(x => x.Name == name && x.ParentId == parentId))
        {
            name = $"{name}({i++})";
        }

        id ??= SnowFlakeSingle.Instance.NextId();
        db.InsertNav(new LocalComic()
        {
            Id = (long)id,
            Name = name,
            Thumb = "ms-appx:///Assets/Default/folder.png",
            Affiliation = "ShadowViewer.Plugin.Local",
            ParentId = parentId,
            IsFolder = true,
            ReadingRecord = new LocalReadingRecord()
        }).Include(x => x.ReadingRecord).ExecuteCommand();
    }

    #region 排序

    /// <summary>
    /// 字母顺序A-Z
    /// </summary>
    public static int AzSort(LocalComic x, LocalComic y) => x.Name?.CompareTo(y.Name) ?? 1;

    /// <summary>
    /// 字母顺序Z-A
    /// </summary>
    public static int ZaSort(LocalComic x, LocalComic y) => y.Name?.CompareTo(x.Name) ?? 1;

    /// <summary>
    /// 阅读时间早-晚
    /// </summary>
    public static int RaSort(LocalComic x, LocalComic y) => x.UpdatedDateTime.CompareTo(y.UpdatedDateTime);

    /// <summary>
    /// 阅读时间晚-早(默认)
    /// </summary>
    public static int RzSort(LocalComic x, LocalComic y) => y.UpdatedDateTime.CompareTo(x.UpdatedDateTime);

    /// <summary>
    /// 创建时间早-晚
    /// </summary>
    public static int CaSort(LocalComic x, LocalComic y) => x.CreatedDateTime.CompareTo(y.CreatedDateTime);

    /// <summary>
    /// 创建时间晚-早
    /// </summary>
    public static int CzSort(LocalComic x, LocalComic y) => y.CreatedDateTime.CompareTo(x.CreatedDateTime);

    /// <summary>
    /// 阅读进度小-大
    /// </summary>
    public static int PaSort(LocalComic x, LocalComic y) => x.ReadingRecord.Percent.CompareTo(y.ReadingRecord.Percent);

    /// <summary>
    /// 阅读进度大-小
    /// </summary>
    public static int PzSort(LocalComic x, LocalComic y) => y.ReadingRecord.Percent.CompareTo(x.ReadingRecord.Percent);

    #endregion
}
