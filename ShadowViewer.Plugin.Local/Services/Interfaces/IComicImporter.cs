using Microsoft.UI.Dispatching;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using ShadowViewer.Plugin.Local.Entities;
using ShadowViewer.Plugin.Local.Models;

namespace ShadowViewer.Plugin.Local.Services.Interfaces;

/// <summary>
/// 漫画导入的处理器
/// </summary>
public interface IComicImporter : IComicIOer
{
    /// <summary>
    /// 导入漫画
    /// </summary>
    /// <param name="preview">预览对象</param> 
    /// <param name="parentId"></param> 
    /// <param name="dispatcher"></param>
    /// <param name="token"></param>
    /// <param name="progress"></param>
    /// <returns>导入结果</returns>
    Task ImportComic(ComicImportPreview preview, long parentId, DispatcherQueue dispatcher,
        CancellationToken token, IProgress<double>? progress = null);

    /// <summary>
    /// 预览漫画
    /// </summary>
    Task<ComicImportPreview> Preview(IStorageItem item);

    /// <summary>
    /// 支持导入的类型
    /// </summary>
    string[] SupportTypes { get; }
}