using Microsoft.UI.Xaml.Data;
using System;

namespace ShadowViewer.Plugin.Local.Converters;

/// <summary>
/// Int等于某个值
/// </summary>
public class IntEqConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var p = parameter switch
        {
            string s => int.Parse(s),
            int t => t,
            _ => 0
        };
        if (value is int i)
        {
            return i == p;
        }

        return false;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}