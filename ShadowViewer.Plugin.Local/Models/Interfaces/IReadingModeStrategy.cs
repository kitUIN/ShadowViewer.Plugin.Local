using ShadowViewer.Plugin.Local.Controls;
using ShadowViewer.Plugin.Local.Enums;

namespace ShadowViewer.Plugin.Local.Models.Interfaces;

/// <summary>
/// 阅读模式
/// </summary>
public interface IReadingModeStrategy
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader">阅读器</param>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    void OnCurrentIndexChanged(LocalReader reader, int oldValue, int newValue);

    /// <summary>
    /// 下一页
    /// </summary>
    void NextPage(LocalReader reader);

    /// <summary>
    /// 上一页
    /// </summary>
    void PrevPage(LocalReader reader);


    /// <summary>
    /// 允许下一页
    /// </summary>
    bool CanNextPage(LocalReader reader);

    /// <summary>
    /// 允许上一页
    /// </summary>
    bool CanPrevPage(LocalReader reader);
}