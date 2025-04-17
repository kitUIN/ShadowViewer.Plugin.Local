using Microsoft.UI.Dispatching;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using ShadowViewer.Plugin.Local.Models;

namespace ShadowViewer.Plugin.Local.Services.Interfaces;

/// <summary>
/// 漫画导入导出的处理器
/// </summary>
public interface IComicIOer
{
    /// <summary>
    /// 类别(插件Id)
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// 检查是否用于识别
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    bool Check(IStorageItem item);

    /// <summary>
    /// 导入漫画
    /// </summary>
    /// <param name="item">文件</param> 
    /// <param name="parentId"></param> 
    /// <param name="dispatcher"></param>
    /// <param name="token"></param>
    /// <returns>导入结果</returns>
    Task ImportComic(IStorageItem item, long parentId, DispatcherQueue dispatcher,
        CancellationToken token);

    /// <summary>
    /// 导出漫画
    /// </summary>
    /// <param name="outputPath"></param>
    /// <param name="comic">文件</param>
    /// <param name="exportType"></param>
    /// <param name="dispatcher"></param>
    /// <param name="token"></param>
    /// <returns>导出结果</returns>
    Task ExportComic(string outputPath, LocalComic comic, string exportType, DispatcherQueue dispatcher, CancellationToken token);

    /// <summary>
    /// 支持的导出模式
    /// </summary>
    string[] SupportExportTypes { get; }
}