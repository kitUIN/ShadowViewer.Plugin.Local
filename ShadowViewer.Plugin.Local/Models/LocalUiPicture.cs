using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

public partial class LocalUiPicture : ObservableObject, IUiPicture
{
    [ObservableProperty] public partial int Index { get; set; }

    [ObservableProperty] public partial string SourcePath { get; set; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="index"></param>
    /// <param name="uri"></param>
    public LocalUiPicture(int index, Uri uri)
    {
        Index = index;
        SourcePath = uri.OriginalString;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="index"></param>
    /// <param name="uri"></param>
    public LocalUiPicture(int index, string uri)
    {
        Index = index;
        SourcePath = uri;
    }
}