using ShadowPluginLoader.Attributes;

namespace ShadowViewer.Plugin.Local.Enums;

/// <summary>
/// 设置Key
/// </summary>
[ShadowSettingClass(Container = "ShadowViewer.Plugin.Local", ClassName = "LocalSettings")]
public enum LocalSettingKey
{
    /// <summary>
    /// 允许相同文件夹导入
    /// </summary>
    [ShadowSetting(typeof(bool), "false", "允许相同文件夹导入")]
    LocalIsImportAgain,

    /// <summary>
    /// 显示书架左下角信息栏
    /// </summary>
    [ShadowSetting(typeof(bool), "true", "显示书架左下角信息栏")]
    LocalIsBookShelfInfoBar,

    /// <summary>
    /// 删除漫画同时删除漫画缓存
    /// </summary>
    [ShadowSetting(typeof(bool), "false", "删除漫画同时删除漫画缓存")]
    LocalIsDeleteFilesWithComicDelete,

    /// <summary>
    /// 删除二次确认
    /// </summary>
    [ShadowSetting(typeof(bool), "false", "删除二次确认")]
    LocalIsRememberDeleteFilesWithComicDelete,

    /// <summary>
    /// 书架-样式-详细/简约
    /// </summary>
    [ShadowSetting(typeof(bool), "false", "书架-样式-详细/简约")]
    LocalBookStyleDetail,

    /// <summary>
    /// 阅读模式-滑动/双页
    /// </summary>
    [ShadowSetting(typeof(LocalReadMode),  comment: "阅读模式-滑动/双页")]
    LocalReaderMode,
}