using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using ShadowViewer.Core.Services;
using ShadowViewer.Plugin.Local.Models;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// 漫画导入导出服务
/// </summary>
public partial class ComicIoService
{
    /// <summary>
    /// 导入器
    /// </summary>
    [Autowired]
    private IEnumerable<IComicImporter> Importers { get; }

    /// <summary>
    /// 导出器
    /// </summary>
    [Autowired]
    private IEnumerable<IComicExporter> Exporters { get; }

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
        foreach (var importer in Importers.OrderBy(x => x.Priority))
        {
            if (importer.Check(item)) return importer;
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

    /// <summary>
    /// 支持的导入格式
    /// </summary>
    /// <returns></returns>
    public string[] GetImportSupportType()
    {
        var result = new List<string>();
        foreach (var importer in Importers)
        {
            result.AddRange(importer.SupportTypes);
        }

        return result.ToArray();
    }

    /// <summary>
    /// 支持的导出格式
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, IList<string>> GetExportSupportType()
    {
        return Exporters.SelectMany(exporter => exporter.SupportTypes)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public IComicExporter GetExporter(IStorageItem item)
    {
        foreach (var exporter in Exporters.OrderBy(x => x.Priority))
        {
            if (exporter.Check(item)) return exporter;
        }

        throw new NotSupportedException($"No exporter found for file: {item.Name}");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <param name="comic"></param> 
    /// <param name="dispatcher"></param>
    /// <param name="token"></param>
    public async Task Export(IStorageItem item, LocalComic comic,
        DispatcherQueue dispatcher, CancellationToken token)
    {
        try
        {
            await GetExporter(item).ExportComic((StorageFile)item, comic, dispatcher, token);
        }
        catch (Exception ex)
        {
            NotifyService.NotifyTip(this, $"{ex}", InfoBarSeverity.Error);
        }
    }
}