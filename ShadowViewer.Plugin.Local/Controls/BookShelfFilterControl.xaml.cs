using ShadowViewer.Plugin.Local.ViewModels;

namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 书架筛选条件的编辑面板。
/// </summary>
public sealed partial class BookShelfFilterControl
{
    /// <summary>当前编辑副本。</summary>
    public BookShelfFilterViewModel ViewModel { get; }

    /// <summary>使用编辑副本创建面板。</summary>
    public BookShelfFilterControl(BookShelfFilterViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
