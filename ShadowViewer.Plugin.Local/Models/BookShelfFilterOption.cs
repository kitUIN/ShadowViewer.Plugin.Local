using CommunityToolkit.Mvvm.ComponentModel;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 筛选面板中的标签或作者选项。
/// </summary>
public partial class BookShelfFilterOption(long id, string name, bool isSelected) : ObservableObject
{
    /// <summary>标签或作者的数据库 ID。</summary>
    public long Id { get; } = id;
    /// <summary>显示名称。</summary>
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
