using ShadowViewer.Plugin.Local.Readers;
using ShadowViewer.Sdk.Converters;

namespace ShadowViewer.Plugin.Local.Converters;

public class WidePageSplitScopeConverter : MenuFlyoutEnumItemConverter<WidePageSplitScope>
{
    protected override string GetI18N(string value) =>
        I18n.ResourcesHelper.GetString("WidePageSplitScope" + value) ?? value;
}
