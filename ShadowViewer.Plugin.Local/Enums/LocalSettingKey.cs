using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadowViewer.Plugin.Local.Enums
{
    public enum LocalSettingKey
    {
        /// <summary>
        /// 允许相同文件夹导入
        /// </summary>
        LocalIsImportAgain,
        LocalIsBookShelfInfoBar,
        /// <summary>
        /// 删除漫画同时删除漫画缓存
        /// </summary>
        LocalIsDeleteFilesWithComicDelete,
        /// <summary>
        /// 删除二次确认
        /// </summary>
        LocalIsRememberDeleteFilesWithComicDelete,
        /// <summary>
        /// 书架-样式-详细/简约
        /// </summary>
        LocalBookStyleDetail,
    }
}
