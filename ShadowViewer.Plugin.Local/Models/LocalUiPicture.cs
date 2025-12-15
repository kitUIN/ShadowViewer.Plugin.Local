using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

public partial class LocalUiPicture : ObservableObject, IUiPicture
{
    [ObservableProperty] public partial int Index { get; set; }
    [ObservableProperty] public partial ImageSource Source { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <param name="image"></param>
    public LocalUiPicture(int index, BitmapImage image)
    {
        Index = index;
        Source = image;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <param name="uri"></param>
    public LocalUiPicture(int index, Uri uri) : this(index, new BitmapImage() { UriSource = uri })
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <param name="uri"></param>
    public LocalUiPicture(int index, string uri) : this(index, new BitmapImage() { UriSource = new Uri(uri) })
    {
    }
}