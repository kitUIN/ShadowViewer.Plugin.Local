using ShadowPluginLoader.Attributes;
using Windows.Storage;

namespace ShadowViewer.Plugin.Local.Enums;

/// <summary>
/// 设置Key
/// </summary>
[ShadowSettingClass(Container = "ShadowViewer.Plugin.Local", ClassName = "LocalSettings")]
public enum LocalSettingKey
{
    /// <summary>
    /// 自动翻页
    /// </summary>
    [ShadowSetting(typeof(bool), "false", "自动翻页")]
    PageAutoTurn,

    /// <summary>
    /// 自动翻页间隔秒数
    /// </summary>
    [ShadowSetting(typeof(double), "8", "自动翻页间隔秒数")]
    PageAutoTurnInterval,
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
    [ShadowSetting(typeof(LocalReaderMode), "ShadowViewer.Plugin.Local.Enums.LocalReaderMode.DoublePage",  comment: "阅读模式-滑动/双页")]
    LocalReaderMode,
    /// <summary>
    /// 阅读器点击区域设置
    /// </summary>
    [ShadowSetting(typeof(ApplicationDataCompositeValue), "new Windows.Storage.ApplicationDataCompositeValue()",  comment: "阅读器点击区域设置")]
    TappedGridLayout,
}