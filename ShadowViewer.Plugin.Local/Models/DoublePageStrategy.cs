using System.Collections.Generic;
using ShadowViewer.Plugin.Local.Controls;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 
/// </summary>
public class DoublePageStrategy : IReadingModeStrategy
{
    /// <inheritdoc />
    public void OnCurrentIndexChanged(LocalReader reader, int oldValue, int newValue)
    {
        if (reader.CurrentIndex <= 0 || reader.CurrentIndex > reader.Pictures.Count) return;
        var index = reader.CurrentIndex;
        if (index % 2 == 0) index -= 1;
        
        var sources = new List<string>();
        if (reader.Pictures.Count >= index)
        {
            var leftPicture = reader.Pictures[index - 1];
            sources.Add(leftPicture.SourcePath);
        }

        if (reader.Pictures.Count >= index + 1)
        {
            var rightPicture = reader.Pictures[index];
            sources.Add(rightPicture.SourcePath);
        }

        reader.Sources = sources;

        reader.CheckCanPage();
    }

    /// <inheritdoc />
    public void NextPage(LocalReader reader)
    {
        if (CanNextPage(reader)) reader.CurrentIndex += 2;
    }

    /// <inheritdoc />
    public void PrevPage(LocalReader reader)
    {
        if (CanPrevPage(reader)) reader.CurrentIndex -= 2;
    }

    /// <inheritdoc />
    public bool CanNextPage(LocalReader reader)
    {
        return reader.CurrentIndex + 2 <= reader.Pictures.Count;
    }

    /// <inheritdoc />
    public bool CanPrevPage(LocalReader reader)
    {
        return reader.CurrentIndex - 2 >= 1;
    }
}