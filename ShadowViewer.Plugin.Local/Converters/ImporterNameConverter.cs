using Microsoft.UI.Xaml.Data;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using System;

namespace ShadowViewer.Plugin.Local.Converters
{
    public class ImporterNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is IComicImporter importer)
            {
                var typeName = importer.GetType().Name;
                return typeName.EndsWith("ComicImporter")
                    ? typeName.Substring(0, typeName.Length - "ComicImporter".Length)
                    : typeName;
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
