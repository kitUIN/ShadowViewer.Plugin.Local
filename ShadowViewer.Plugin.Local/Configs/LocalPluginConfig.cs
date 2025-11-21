using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using ShadowObservableConfig.Attributes;
using ShadowPluginLoader.WinUI;
using ShadowViewer.Plugin.Local.Enums;
using Windows.Storage;

namespace ShadowViewer.Plugin.Local.Configs;

[ObservableConfig(FileName = "local_plugin_config")]
public partial class LocalPluginConfig
{
    /// <summary>
    /// 漫画缓存文件夹地址
    /// </summary>
    [ObservableConfigProperty(Description = "漫画缓存文件夹地址")]
    private string comicFolder = "comic";

    /// <summary>
    /// 漫画缓存文件夹地址
    /// </summary>
    public string ComicFolderPath => Path.Combine(StaticValues.BaseFolder, comicFolder);

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
    private int bookShelfStyle;

    /// <summary>
    /// 阅读模式-滑动/双页
    /// </summary>
    [ObservableConfigProperty(Description = "阅读模式-滑动/双页")]
    private LocalReaderMode localReaderMode = LocalReaderMode.DoublePage;

    /// <summary>
    /// 阅读器点击区域设置
    /// </summary>
    [ObservableConfigProperty(Description = "阅读器点击区域设置")]
    private Dictionary<string, double>? tappedGridLayout;
}