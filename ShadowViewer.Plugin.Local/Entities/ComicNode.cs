using DryIoc;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShadowViewer.Plugin.Local.Entities;

/// <summary>
/// 统一节点表（文件夹和漫画的通用节点）
/// </summary>
[SugarIndex("index_comic_node_parent_id", nameof(ParentId), OrderByType.Asc)]
[SugarIndex("index_comic_node_created_at", nameof(CreatedDateTime), OrderByType.Asc)]
[SugarIndex("index_comic_node_updated_at", nameof(UpdatedDateTime), OrderByType.Asc)]
[SugarIndex("index_comic_node_name", nameof(Name), OrderByType.Asc)]
[SugarIndex("index_comic_node_type", nameof(NodeType), OrderByType.Asc)]
public class ComicNode : IComicNode
{
    /// <summary>
    /// Id
    /// </summary>

    [SugarColumn(IsPrimaryKey = true)]
    public long Id { get; set; }

    /// <summary>
    /// 父Id
    /// </summary>

    [SugarColumn(ColumnDescription = "父Id", DefaultValue = "-1")]
    public long ParentId { get; set; }

    /// <summary>
    /// 节点类型 (Folder / Comic)
    /// </summary>

    [SugarColumn(ColumnDataType = "varchar(32)", ColumnDescription = "节点类型", IsNullable = false)]
    public string NodeType { get; set; } = "Folder";

    /// <summary>
    /// 名称
    /// </summary>

    [SugarColumn(ColumnDataType = "Nvarchar(500)", ColumnDescription = "名称", IsNullable = false)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// 缩略图
    /// </summary>

    [SugarColumn(ColumnDataType = "TEXT", DefaultValue = "mx-appx:///default.png", ColumnDescription = "缩略图")]
    public string Thumb { get; set; } = "mx-appx:///default.png";

    /// <summary>
    /// 创建时间
    /// </summary>

    [SugarColumn(InsertServerTime = true, ColumnDescription = "创建时间")]
    public DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>

    [SugarColumn(InsertServerTime = true, UpdateServerTime = true, ColumnDescription = "更新时间")]
    public DateTime UpdatedDateTime { get; set; }
    /// <summary>
    /// Gets or sets the source plugin data identifier.
    /// </summary>
    [SugarColumn(IsNullable = true)] public string? SourcePluginDataId { get; set; }

    /// <summary>
    /// 归属的插件
    /// </summary>
    [Navigate(NavigateType.OneToOne, nameof(SourcePluginDataId))]
    public SourcePluginData? SourcePluginData { get; set; }

    /// <summary>
    /// 归属的插件
    /// </summary>
    [Navigate(NavigateType.OneToOne, nameof(Id))]
    public ComicDetail? ComicDetail { get; set; }


    /// <summary>
    /// 
    /// </summary>
    public bool IsFolder => NodeType == "Folder";

    /// <summary>
    /// 文件大小
    /// </summary>

    [SugarColumn(ColumnDescription = "文件大小")]
    public long Size { get; set; }

    /// <summary>
    /// 是否损坏
    /// </summary>

    [SugarColumn(ColumnDescription = "是否损坏")]
    public bool IsBroken { get; set; }

    /// <summary>
    /// 是否损坏
    /// </summary>

    [SugarColumn(ColumnDescription = "损坏原因", IsNullable = true)]
    public string? BrokenReason { get; set; }

    /// <summary>
    /// 是否删除
    /// </summary>

    [SugarColumn(ColumnDescription = "是否删除")]
    public bool IsDelete { get; set; }

    /// <summary>
    /// 阅读记录
    /// </summary>
    [Navigate(NavigateType.OneToOne, nameof(Id))]
    public LocalReadingRecord ReadingRecord { get; set; } = null!;

    /// <summary>
    /// 
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public ICollection<IComicNode> Children { get; } = new ObservableCollection<IComicNode>();

    /// <summary>
    /// 预览使用的章节列表
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public List<ComicChapter> PreviewChapters { get; set; } = new();

    /// <summary>
    /// 新建文件夹
    /// </summary>
    /// <param name="name">文件夹名称</param>
    /// <param name="parentId">父级Id</param>
    /// <param name="id"></param>
    public static void CreateFolder(string? name, long parentId = -1, long? id = null)
    {
        var db = DiFactory.Services.Resolve<ISqlSugarClient>();

        // 1. 基础名称处理
        var baseName = string.IsNullOrWhiteSpace(name) ? I18N.NewFolder : name;
        var finalName = baseName;

        var existingNames = db.Queryable<ComicNode>()
            .Where(x => x.ParentId == parentId && x.Name.StartsWith(baseName))
            .Select(x => x.Name)
            .ToList();

        if (existingNames.Contains(baseName))
        {
            var suffix = 1;
            // 在内存中快速循环，不再频繁访问数据库
            while (existingNames.Contains($"{baseName}({suffix})"))
            {
                suffix++;
            }

            finalName = $"{baseName}({suffix})";
        }

        // 3. 组装对象
        var newNode = new ComicNode
        {
            Id = id ?? SnowFlakeSingle.Instance.NextId(),
            Name = finalName,
            NodeType = "Folder",
            Thumb = "ms-appx:///Assets/Default/folder.png",
            ParentId = parentId
        };

        // 4. 执行插入
        db.InsertNav(newNode).Include(x => x.ReadingRecord).ExecuteCommand();
    }
}