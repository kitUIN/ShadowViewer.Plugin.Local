using System.Linq;
using DryIoc;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using Microsoft.EntityFrameworkCore;
using ShadowViewer.Plugin.Local.Database;
using ShadowViewer.Sdk.Database;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShadowViewer.Plugin.Local.Entities;

/// <summary>
/// 统一节点表（文件夹和漫画的通用节点）
/// </summary>
public class ComicNode : IComicNode
{
    /// <summary>
    /// Id
    /// </summary>

    public long Id { get; set; }

    /// <summary>
    /// 父Id
    /// </summary>

    public long ParentId { get; set; }

    /// <summary>
    /// 节点类型 (Folder / Comic)
    /// </summary>

    public string NodeType { get; set; } = "Folder";

    /// <summary>
    /// 名称
    /// </summary>

    public string Name { get; set; } = null!;

    /// <summary>
    /// 缩略图
    /// </summary>

    public string Thumb { get; set; } = "mx-appx:///default.png";

    /// <summary>
    /// 创建时间
    /// </summary>

    public DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>

    public DateTime UpdatedDateTime { get; set; }
    /// <summary>
    /// Gets or sets the source plugin data identifier.
    /// </summary>
    public string? SourcePluginDataId { get; set; }

    /// <summary>
    /// 归属的插件
    /// </summary>
    public SourcePluginData? SourcePluginData { get; set; }

    /// <summary>
    /// 归属的插件
    /// </summary>
    public ComicDetail? ComicDetail { get; set; }


    /// <summary>
    /// 
    /// </summary>
    public bool IsFolder => NodeType == "Folder";

    /// <summary>
    /// 文件大小
    /// </summary>

    public long Size { get; set; }

    /// <summary>
    /// 是否损坏
    /// </summary>

    public bool IsBroken { get; set; }

    /// <summary>
    /// 是否损坏
    /// </summary>

    public string? BrokenReason { get; set; }

    /// <summary>
    /// 是否删除
    /// </summary>

    public bool IsDelete { get; set; }

    /// <summary>
    /// 阅读记录
    /// </summary>
    public LocalReadingRecord ReadingRecord { get; set; } = null!;

    /// <summary>
    /// 
    /// </summary>
    public ICollection<IComicNode> Children { get; } = new ObservableCollection<IComicNode>();

    /// <summary>
    /// 预览使用的章节列表
    /// </summary>
    public List<ComicChapter> PreviewChapters { get; set; } = new();

    /// <summary>
    /// 新建文件夹
    /// </summary>
    /// <param name="name">文件夹名称</param>
    /// <param name="parentId">父级Id</param>
    /// <param name="id"></param>
    public static void CreateFolder(string? name, long parentId = -1, long? id = null)
    {
        using var db = DiFactory.Services.Resolve<IDbContextFactory<LocalDbContext>>().CreateDbContext();

        // 1. 基础名称处理
        var baseName = string.IsNullOrWhiteSpace(name) ? I18N.NewFolder : name;
        var finalName = baseName;

        var existingNames = db.Set<ComicNode>()
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
            Id = id ?? DatabaseIds.Next(),
            Name = finalName,
            NodeType = "Folder",
            Thumb = "ms-appx:///Assets/Default/folder.png",
            ParentId = parentId,
            ReadingRecord = new LocalReadingRecord()
        };

        // 4. 执行插入
        db.Add(newNode);
        db.SaveChanges();
    }
}
