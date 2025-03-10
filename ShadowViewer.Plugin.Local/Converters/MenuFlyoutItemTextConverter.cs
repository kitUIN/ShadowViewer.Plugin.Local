using System;
using Microsoft.UI.Xaml.Data;

namespace ShadowViewer.Plugin.Local.Converters;

/// <summary>
/// 用于转换MenuFlyoutItem的Text
/// </summary>
public class MenuFlyoutItemTextConverter:IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return I18n.ResourcesHelper.GetString(value.ToString());
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}