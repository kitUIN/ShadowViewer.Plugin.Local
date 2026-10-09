// Only Windows UI / plugin host types are adapted. EF, SQLite, DryIoc and entity sources are real.
namespace Microsoft.UI.Xaml.Media
{
    public class Brush { }
    public class SolidColorBrush(object color) : Brush { public object Color { get; } = color; }
}

namespace CommunityToolkit.WinUI.Helpers
{
    public static class ColorExtensions
    {
        public static object ToColor(this string value) => value;
    }
}

namespace ShadowPluginLoader.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MetaAttribute : Attribute
    {
        public bool Exclude { get; set; }
        public bool Required { get; set; }
    }
}

namespace ShadowPluginLoader.WinUI
{
    public static class DiFactory
    {
        public static DryIoc.IContainer Services { get; set; } = new DryIoc.Container();
    }
}

namespace ShadowViewer.Sdk.Models.Interfaces
{
    public interface IShadowTag { }
    public interface IHistory { }
}

namespace ShadowViewer.Plugin.Local.Models.Interfaces
{
    public interface IComicNode { }
    public interface IAuthor { }
    public interface IReadingRecord { }
}

namespace ShadowViewer.Plugin.Local.I18n
{
    public static class I18N
    {
        public static string NewFolder => "New folder";
    }
}

namespace ShadowViewer.Sdk.Helpers
{
    public static class EncryptingHelper
    {
        public static string CreateMd5(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes));
    }
}
