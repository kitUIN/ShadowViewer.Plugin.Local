using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using ShadowViewer.Core.Services;
using ShadowViewer.Plugin.Local.I18n;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 漫画导入工厂
/// </summary>
public partial class ComicImportService
{
    /// <summary>
    /// 导入器
    /// </summary>
    [Autowired]
    private IEnumerable<IComicImporter> Importers { get; }

    /// <summary>
    /// NotifyService
    /// </summary>
    [Autowired]
    private INotifyService NotifyService { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public IComicImporter GetImporter(IStorageItem item)
    {
        foreach (var importer in Importers)
        {
            if (importer.Check(item))  return importer;
        }

        throw new NotSupportedException($"No importer found for file: {item.Name}");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <param name="parentId"></param>
    /// <param name="dispatcher"></param>
    /// <param name="token"></param>
    public async Task Import(IStorageItem item, long parentId,
        DispatcherQueue dispatcher, CancellationToken token)
    {
        try
        {
            await GetImporter(item).ImportComic(item, parentId, dispatcher, token); 
        }
        catch (Exception ex)
        {
            NotifyService.NotifyTip(this, $"{ex}", InfoBarSeverity.Error);
        }
    }
}