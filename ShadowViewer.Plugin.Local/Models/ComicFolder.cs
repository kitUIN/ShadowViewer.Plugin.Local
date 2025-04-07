using ShadowPluginLoader.Attributes;
using ShadowViewer.Core.Models.Interfaces;
using ShadowViewer.Core.Plugins;
using ShadowViewer.Core.Settings;
using ShadowViewer.Plugin.Local.I18n;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 
/// </summary>
[EntryPoint(Name = nameof(PluginResponder.SettingFolders))]
public partial class ComicFolder : ISettingFolder
{
    /// <inheritdoc />
    [Autowired]
    public string PluginId { get; } = null!;

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
    public bool CanOpen => true;

    /// <inheritdoc />
    public bool CanChange => false;
}