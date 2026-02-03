using Microsoft.UI.Xaml.Media;

namespace ShadowViewer.Plugin.Local.Models.Interfaces;

/// <summary>
/// 漫画-页 基类
/// </summary>
public interface IUiPicture
{
    /// <summary>
    /// 序号(请从1开始)
    /// </summary>
    int Index { get; set; }
    /// <summary>
    /// 图片
    /// </summary>
    ImageSource Source { get; set; }
    /// <summary>
    /// 图片文件路径
    /// </summary>
    string SourcePath { get; set; }
}