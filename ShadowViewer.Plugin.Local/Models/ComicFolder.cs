using ShadowPluginLoader.Attributes;
using ShadowViewer.Plugin.Local.I18n;
using ShadowViewer.Sdk.Models.Interfaces;
using ShadowViewer.Sdk.Plugins;

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
    public string Path { get; set; }

    /// <inheritdoc />
    public bool CanOpen => true;

    /// <inheritdoc />
    public bool CanChange => false;
}