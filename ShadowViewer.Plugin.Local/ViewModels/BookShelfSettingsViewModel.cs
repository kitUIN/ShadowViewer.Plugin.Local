using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Configs;
using ShadowViewer.Helpers;
using ShadowViewer.Plugin.Local.Enums;

namespace ShadowViewer.Plugin.Local.ViewModels
{
    partial class BookShelfSettingsViewModel : ObservableObject
    {
        /// <summary>
        /// 删除二次确认
        /// </summary>
        [ObservableProperty]
        private bool isRememberDeleteFilesWithComicDelete = !ConfigHelper.GetBoolean(LocalSettingKey.LocalIsRememberDeleteFilesWithComicDelete);
        /// <summary>
        /// 删除漫画同时删除漫画缓存
        /// </summary>
        [ObservableProperty] private bool isDeleteFilesWithComicDelete = ConfigHelper.GetBoolean(LocalSettingKey.LocalIsDeleteFilesWithComicDelete);
        
        [ObservableProperty] private bool isBookShelfInfoBar = ConfigHelper.GetBoolean(LocalSettingKey.LocalIsBookShelfInfoBar);
        /// <summary>
        /// 允许相同文件夹导入
        /// </summary>
        [ObservableProperty] private bool isImportAgain = ConfigHelper.GetBoolean(LocalSettingKey.LocalIsImportAgain);

        partial void OnIsImportAgainChanged(bool oldValue, bool newValue)
        {
            if (oldValue != newValue)
            {
                ConfigHelper.Set(LocalSettingKey.LocalIsRememberDeleteFilesWithComicDelete, IsRememberDeleteFilesWithComicDelete);
            }   
        }

        partial void OnIsBookShelfInfoBarChanged(bool oldValue, bool newValue)
        {
            if (oldValue != newValue)
            {
                ConfigHelper.Set(LocalSettingKey.LocalIsBookShelfInfoBar, IsBookShelfInfoBar);
            }
        }
        partial void OnIsDeleteFilesWithComicDeleteChanged(bool oldValue, bool newValue)
        {
            if (oldValue != newValue)
            {
                ConfigHelper.Set(LocalSettingKey.LocalIsDeleteFilesWithComicDelete, IsDeleteFilesWithComicDelete);
            }
        }

        partial void OnIsRememberDeleteFilesWithComicDeleteChanged(bool oldValue, bool newValue)
        {
            if (oldValue != newValue)
            {
                ConfigHelper.Set(LocalSettingKey.LocalIsRememberDeleteFilesWithComicDelete, IsRememberDeleteFilesWithComicDelete);
            }
        }
    }
}
