using System;
using System.Collections.Generic;

namespace ShadowViewer.Plugin.Local.Entities;

/// <summary>
/// 插件源数据实体类
/// </summary>
public class SourcePluginData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourcePluginData"/> class.
    /// </summary>
    public SourcePluginData()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SourcePluginData"/> class.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="version">The version.</param>
    /// <param name="name"></param>
    /// <param name="backgroundColorHex">The background color hexadecimal.</param>
    /// <param name="foregroundColorHex">The foreground color hexadecimal.</param>
    public SourcePluginData(string pluginId, string version, string name, string backgroundColorHex, string foregroundColorHex)
    {
        Id = pluginId + version;
        PluginId = pluginId;
        DataVersion = version;
        Name = name;
        BackgroundColorHex = backgroundColorHex;
        ForegroundColorHex = foregroundColorHex;
    }

    /// <summary>
    /// 主键 ID
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// 插件或数据源Id
    /// </summary>
    public string PluginId { get; set; } = null!;

    /// <summary>
    /// 插件或数据源名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 数据版本号，用于处理兼容性或同步
    /// </summary>
    public string DataVersion { get; set; } = null!;

    /// <summary>
    /// 背景颜色十六进制值（例如：#FFFFFF）
    /// </summary>
    public string BackgroundColorHex { get; set; } = "#FFFFFF";

    /// <summary>
    /// 前景颜色十六进制值
    /// </summary>
    public string ForegroundColorHex { get; set; } = "#000000";

    /// <summary>
    /// 扩展数据。在数据库以 JSON 字符串存储，
    /// </summary>
    public Dictionary<string, object>? ExtraData { get; set; }
}
