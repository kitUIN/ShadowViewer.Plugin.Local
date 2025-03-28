using ShadowViewer.Core.Models.Interfaces;
using ShadowViewer.Core.Settings;
using ShadowViewer.Plugin.Local.I18n;

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
        get => CoreSettings.Instance.ComicsPath;
        set => CoreSettings.Instance.ComicsPath = value;
    }

    /// <inheritdoc />
    public bool CanOpen   => true;

    /// <inheritdoc />
    public bool CanChange => false;
}