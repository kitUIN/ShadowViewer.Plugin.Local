using Microsoft.UI.Dispatching;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace ShadowViewer.Plugin.Local.Services.Interfaces;

/// <summary>
/// 漫画导入的处理器
/// </summary>
public interface IComicImporter : IComicIOer
{ 
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

}