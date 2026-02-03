using System.Collections.Generic;
using ShadowViewer.Plugin.Local.Controls;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 
/// </summary>
public class SinglePageStrategy: IReadingModeStrategy
{
    /// <inheritdoc />
    public void OnCurrentIndexChanged(LocalReader reader, int oldValue, int newValue)
    {
        if (reader.CurrentIndex <= 0 || reader.CurrentIndex > reader.Pictures.Count) return;
        if (reader.Pictures.Count >= reader.CurrentIndex)
        {
            var picture = reader.Pictures[reader.CurrentIndex - 1];
            reader.Sources = new List<string> { picture.SourcePath };
        }
        reader.CheckCanPage();
    }

    /// <inheritdoc />
    public void NextPage(LocalReader reader)
    {
        if (CanNextPage(reader)) reader.CurrentIndex += 1;
    }

    /// <inheritdoc />
    public void PrevPage(LocalReader reader)
    {
        if (CanPrevPage(reader)) reader.CurrentIndex -= 1;
    }

    /// <inheritdoc />
    public bool CanNextPage(LocalReader reader)
    {
        return reader.CurrentIndex + 1 <= reader.Pictures.Count;
    }

    /// <inheritdoc />
    public bool CanPrevPage(LocalReader reader)
    {
        return reader.CurrentIndex - 1 >= 1;
    }
}