using Windows.Storage;
using ShadowObservableConfig.Attributes;
using ShadowViewer.Plugin.Local.Enums;

namespace ShadowViewer.Plugin.Local.Configs;

[ObservableConfig(FileName = "local_plugin_config")]
public partial class LocalPluginConfig
{
    /// <summary>
    /// 自动翻页
    /// </summary>
    [ObservableConfigProperty(Description = "自动翻页")]
    private bool pageAutoTurn;

    /// <summary>
    /// 自动翻页间隔秒数
    /// </summary>
    [ObservableConfigProperty(Description = "自动翻页间隔秒数")]
    private double pageAutoTurnInterval = 8;

    /// <summary>
    /// 允许相同文件夹导入
    /// </summary>
    [ObservableConfigProperty(Description = "允许相同文件夹导入")]
    private bool localIsImportAgain;

    /// <summary>
    /// 显示书架左下角信息栏
    /// </summary>
    [ObservableConfigProperty(Description = "显示书架左下角信息栏")]
    private bool localIsBookShelfInfoBar = true;

    /// <summary>
    /// 删除漫画同时删除漫画缓存
    /// </summary>
    [ObservableConfigProperty(Description = "删除漫画同时删除漫画缓存")]
    private bool localIsDeleteFilesWithComicDelete;

    /// <summary>
    /// 删除二次确认
    /// </summary>
    [ObservableConfigProperty(Description = "删除二次确认")]
    private bool localIsRememberDeleteFilesWithComicDelete;

    /// <summary>
    /// 书架-样式-详细/简约
    /// </summary>
    [ObservableConfigProperty(Description = "书架-样式-详细/简约")]
    private bool localBookStyleDetail;

    /// <summary>
    /// 阅读模式-滑动/双页
    /// </summary>
    [ObservableConfigProperty(Description = "阅读模式-滑动/双页")]
    private LocalReaderMode localReaderMode = LocalReaderMode.DoublePage;

    /// <summary>
    /// 阅读器点击区域设置
    /// </summary>
    [ObservableConfigProperty(Description = "阅读器点击区域设置")]
    private ApplicationDataCompositeValue tappedGridLayout;
}