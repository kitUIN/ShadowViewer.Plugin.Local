using ShadowViewer.Configs;
using ShadowViewer.Models.Interfaces;
using ShadowViewer.Plugin.Local.Helpers;

namespace ShadowViewer.Plugin.Local.Models;

public class ComicFolder: ISettingFolder
{
    public ComicFolder(string pluginId)
    {
        PluginId = pluginId;
    }

    /// <inheritdoc />
    public string PluginId { get; }

    /// <inheritdoc />
    public string Name => I18N.ComicFolder;

    /// <inheritdoc />
    public string Description => I18N.ComicFolderDescription;

    /// <inheritdoc />
    public string Path
    {
        get => Config.ComicsPath;
        set => Config.ComicsPath = value;
    }

    /// <inheritdoc />
    public bool CanOpen   => true;

    /// <inheritdoc />
    public bool CanChange => false;
}