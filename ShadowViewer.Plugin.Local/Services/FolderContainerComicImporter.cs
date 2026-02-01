using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk.Helpers;
using ShadowViewer.Sdk.Models;
using ShadowViewer.Sdk.Services;
using SqlSugar;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 文件夹(多漫画)类型导入器
/// </summary>
[CheckAutowired]
public partial class FolderContainerComicImporter : FolderComicImporter
{
    /// <inheritdoc/>
    public override string Name => "MultiComicFolder";

    /// <inheritdoc />
    public override int Priority => 1;

    /// <inheritdoc />
    public override async Task<ComicImportPreview> Preview(IStorageItem item)
    {
        if (item is not StorageFolder folder) return new ComicImportPreview();
        var node = ShadowTreeNode.FromFolder(folder.Path);

        var subPreviews = new List<ComicImportPreview>();

        // It is a container folder
        foreach (var child in node.Children.Where(c => c.IsDirectory))
        {
            var childPreview = GetSingleComicPreview(child);
            // Only add valid comics
            if (childPreview.ComicDetail.PageCount > 0)
            {
                subPreviews.Add(childPreview);
            }
        }

        if (subPreviews.Count > 0)
        {
            return new ComicImportPreview()
            {
                Name = folder.DisplayName,
                Thumb = "ms-appx:///Assets/Default/folder.png",
                SubPreviews = subPreviews,
                SourceItem = folder,
                ComicDetail = new ComicDetail()
                {
                    ChapterCount = subPreviews.Sum(x => x.ComicDetail.ChapterCount),
                    PageCount = subPreviews.Sum(x => x.ComicDetail.PageCount)
                }
            };
        }

        return new ComicImportPreview();
    }

    /// <inheritdoc />
    public override async Task ImportComic(ComicImportPreview preview, long parentId, DispatcherQueue dispatcher,
        CancellationToken token, IProgress<double>? progress = null)
    {
        // Handle Multi-Comic Import (Container)
        if (preview.SubPreviews.Count > 0)
        {
            // Create a folder node for the container
            var folderNode = await Db.InsertNav(new ComicNode()
            {
                Name = preview.Name,
                ParentId = parentId,
                NodeType = "Folder",
                Thumb = "ms-appx:///Assets/Default/folder.png",  // Default folder icon
                ReadingRecord = new LocalReadingRecord() { CreatedDateTime = DateTime.Now, UpdatedDateTime = DateTime.Now },
                SourcePluginDataId = PluginId + Version
            }).Include(x => x.ReadingRecord).ExecuteReturnEntityAsync();

            int i = 0;
            int total = preview.SubPreviews.Count;
            foreach (var sub in preview.SubPreviews)
            {
                await ImportDetail(sub, folderNode.Id);
                i++;
                progress?.Report(i * 100.0 / total);
            }

            NotifyService.NotifyTip(this, I18N.ImportComicSuccess, InfoBarSeverity.Success);
        }
    }
}
