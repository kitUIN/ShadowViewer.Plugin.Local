using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

public partial class LocalUiPicture : ObservableObject, IUiPicture
{
    [ObservableProperty] private int index;
    [ObservableProperty] private ImageSource source;

    public LocalUiPicture(int index, BitmapImage image)
    {
        Index = index;
        Source = image;
    }

    public LocalUiPicture(int index, Uri uri) : this(index, new BitmapImage() { UriSource = uri })
    {
    }

    public LocalUiPicture(int index, string uri) : this(index, new BitmapImage() { UriSource = new Uri(uri) })
    {
    }
}