using CommunityToolkit.Mvvm.ComponentModel;
using DryIoc;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Constants;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using ShadowViewer.Sdk.Models;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 本地漫画
/// </summary>
[SugarTable(IsDisabledDelete = true)]
public partial class LocalComic : ObservableObject, IComicNode
{
    #region Field

    /// <summary>
    /// Id
    /// </summary>
    [ObservableProperty]
    public partial long Id { get; set; }

    /// <summary>
    /// 父Id
    /// </summary>
    [ObservableProperty]
    public partial long ParentId { get; set; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [ObservableProperty]
    public partial string NodeType { get; set; } = null!;

    /// <summary>
    /// 漫画Id
    /// </summary>
    [ObservableProperty]
    public partial string? ComicId { get; set; }

    /// <summary>
    /// 漫画名称
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; } = null!;

    /// <summary>
    /// 漫画缩略图
    /// </summary>
    [ObservableProperty]
    public partial string Thumb { get; set; } = null!;

    /// <summary>
    /// 漫画备注
    /// </summary>
    [ObservableProperty]
    public partial string? Remark { get; set; }

    /// <summary>
    /// 路径链接
    /// </summary>
    [ObservableProperty]
    public partial string? Link { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [ObservableProperty]
    public partial DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    [ObservableProperty]
    public partial DateTime UpdatedDateTime { get; set; }

    /// <summary>
    /// 分类
    /// </summary>
    public string Affiliation { get; set; } = null!;


    /// <summary>
    /// 存储空间
    /// </summary>
    [ObservableProperty]
    public partial long Size { get; set; }

    /// <summary>
    /// 话-数量
    /// </summary>
    [ObservableProperty]
    public partial int EpisodeCount { get; set; }

    /// <summary>
    /// 页-数量
    /// </summary>
    [ObservableProperty]
    public partial int Count { get; set; }

    /// <summary>
    /// 是否是文件夹
    /// </summary>
    public bool IsFolder => NodeType == "Folder";

    /// <summary>
    /// 是否删除
    /// </summary>
    [ObservableProperty]
    public partial bool IsDelete { get; set; }

    /// <summary>
    /// 是否损坏
    /// </summary>
    [ObservableProperty]
    public partial bool IsBroken { get; set; }

    /// <summary>
    /// 作者
    /// </summary>
    public ObservableCollection<LocalAuthor> Authors { get; set; } = null!;

    /// <summary>
    /// 标签
    /// </summary>
    public ObservableCollection<ShadowTag> Tags { get; set; } = null!;

    /// <summary>
    /// 阅读记录
    /// </summary>
    public LocalReadingRecord ReadingRecord { get; set; } = null!;

    /// <summary>
    /// Gets the children.
    /// </summary>
    public ICollection<IComicNode> Children { get; } = [];

    #endregion

    #region Constructors

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public LocalComic()
    {
    }

    /// <summary>
    /// 从 ComicNode 和 ComicDetail 构造 LocalComic（用于向后兼容）
    /// </summary>
    /// <param name="node">漫画节点</param>
    /// <param name="dbClient">数据库</param>
    public LocalComic(ComicNode node, ISqlSugarClient? dbClient = null)
    {
        dbClient ??= DiFactory.Services.Resolve<ISqlSugarClient>();

        Id = node.Id;
        ParentId = node.ParentId;
        Name = node.Name;
        Thumb = node.Thumb ?? "appx://Assets/Default.png";
        NodeType = node.NodeType;
        Size = node.Size;
        CreatedDateTime = node.CreatedDateTime;
        UpdatedDateTime = node.UpdatedDateTime;
        ReadingRecord = node.ReadingRecord;
        IsBroken = node.IsBroken;
        IsDelete = node.IsDelete;
        Affiliation = node.SourcePluginData?.PluginId ?? "";

        if (!IsFolder && NodeType == "Comic")
        {
            var detail = dbClient.Queryable<ComicDetail>()
                .Includes(x => x.Authors)
                .Includes(x => x.Tags)
                .Where(x => x.ComicId == Id)
                .First();
            if (detail == null) return;
            ComicId = detail.ExtendId ?? Id.ToString();
            EpisodeCount = detail.ChapterCount;
            Count = detail.PageCount;
            Link = detail.StoragePath;
            Remark = detail.Remark;
            Authors = new ObservableCollection<LocalAuthor>(detail.Authors ?? []);
            Tags = new ObservableCollection<ShadowTag>(detail.Tags ?? []);
        }
        else if (IsFolder)
        {
            // 文件夹默认值
            Affiliation = PluginConstants.PluginId;
            Authors = [];
            Tags = [];
        }
    }

    #endregion
}