using CommunityToolkit.Mvvm.ComponentModel;

namespace ShadowViewer.Plugin.Local.ViewModels;

/// <summary>
/// 设置 ViewModel
/// </summary>
public partial class BookShelfSettingsViewModel : ObservableObject
{
    /// <summary>
    /// 删除二次确认
    /// </summary>
    [ObservableProperty]
    private bool isRememberDeleteFilesWithComicDelete = !LocalPlugin.Settings.LocalIsRememberDeleteFilesWithComicDelete;
    /// <summary>
    /// 删除漫画同时删除漫画缓存
    /// </summary>
    [ObservableProperty] private bool isDeleteFilesWithComicDelete = LocalPlugin.Settings.LocalIsDeleteFilesWithComicDelete;

    [ObservableProperty] private bool isBookShelfInfoBar = LocalPlugin.Settings.LocalIsBookShelfInfoBar;
    /// <summary>
    /// 允许相同文件夹导入
    /// </summary>
    [ObservableProperty] private bool isImportAgain = LocalPlugin.Settings.LocalIsImportAgain;

    partial void OnIsImportAgainChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            LocalPlugin.Settings.LocalIsImportAgain = newValue;
        }
    }

    partial void OnIsBookShelfInfoBarChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            LocalPlugin.Settings.LocalIsBookShelfInfoBar = newValue;
        }
    }
    partial void OnIsDeleteFilesWithComicDeleteChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            LocalPlugin.Settings.LocalIsDeleteFilesWithComicDelete = newValue;
        }
    }

    partial void OnIsRememberDeleteFilesWithComicDeleteChanged(bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            LocalPlugin.Settings.LocalIsRememberDeleteFilesWithComicDelete = !newValue;
        }
    }
}