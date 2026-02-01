using Windows.Storage;

namespace ShadowViewer.Plugin.Local.Services.Interfaces;

/// <summary>
/// 导入导出共有部分
/// </summary>
public interface IComicIOer
{
    /// <summary>
    /// 识别是否可用
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    bool Check(IStorageItem item);

    /// <summary>
    /// 类别(插件Id)
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// 插件版本号
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 优先度,越小越早加载
    /// </summary>
    int Priority { get; }
}