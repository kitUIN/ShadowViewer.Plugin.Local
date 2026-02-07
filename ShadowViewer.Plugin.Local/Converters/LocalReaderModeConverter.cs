using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Sdk.Converters;

namespace ShadowViewer.Plugin.Local.Converters;

/// <summary>
/// 
/// </summary>
public class LocalReaderModeConverter : MenuFlyoutEnumItemConverter<ReadingMode>
{
    /// <summary>
    /// 
    /// </summary>
    protected override string GetI18N(string value)
    {
        return I18n.ResourcesHelper.GetString(value) ?? value;
    }
}