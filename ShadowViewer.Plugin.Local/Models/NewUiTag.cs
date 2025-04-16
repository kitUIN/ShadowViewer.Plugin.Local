using Windows.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml.Media;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 新增标签
/// </summary>
public partial class NewUiTag : ObservableObject
{
    /// <summary>
    /// 字体颜色
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Foreground))]
    private Color foregroundColor = "#000000".ToColor();

    /// <summary>
    /// 背景颜色
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Background))]
    private Color backgroundColor = "#ffd657".ToColor();

    /// <summary>
    /// 背景颜色
    /// </summary>
    public Brush Background => new SolidColorBrush(BackgroundColor);

    /// <summary>
    /// 字体颜色
    /// </summary>
    public Brush Foreground => new SolidColorBrush(ForegroundColor);

    /// <summary>
    /// 图标
    /// </summary>
    [ObservableProperty] private string? icon;

    /// <summary>
    /// 名称
    /// </summary>
    [ObservableProperty] private string name = "";

    /// <summary>
    /// 清理
    /// </summary>
    public void Clean()
    {
        ForegroundColor = "#000000".ToColor();
        BackgroundColor = "#ffd657".ToColor();
        Name = "";
        Icon = null;
    }
}