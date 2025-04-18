using System.Collections.Generic;
using ShadowViewer.Plugin.Local.Models;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Dispatching;

namespace ShadowViewer.Plugin.Local.Services.Interfaces;

/// <summary>
/// 漫画导出的处理器
/// </summary>
public interface IComicExporter : IComicIOer
{
    /// <summary>
    /// 导出漫画
    /// </summary>
    /// <param name="outputItem"></param>
    /// <param name="comic">文件</param> 
    /// <param name="dispatcher"></param>
    /// <param name="token"></param>
    /// <returns>导出结果</returns>
    Task ExportComic(StorageFile outputItem, LocalComic comic, DispatcherQueue dispatcher,
        CancellationToken token);

    /// <summary>
    /// 支持的类型
    /// </summary>
    Dictionary<string, IList<string>> SupportTypes { get; }
}