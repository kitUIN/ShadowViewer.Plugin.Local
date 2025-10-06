using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Sdk.Converters;

namespace ShadowViewer.Plugin.Local.Converters;

/// <summary>
/// 
/// </summary>
public class LocalReaderModeConverter : MenuFlyoutEnumItemConverter<LocalReaderMode>
{
    /// <summary>
    /// 
    /// </summary>
    protected override string GetI18N(string value)
    {
        return I18n.ResourcesHelper.GetString(value);
    }
}