﻿using Microsoft.UI.Xaml.Controls;
using ShadowViewer.Plugin.Local.Controls;
using ShadowViewer.Plugin.Local.Models.Interfaces;

namespace ShadowViewer.Plugin.Local.Models;

/// <summary>
/// 
/// </summary>
public class VerticalScrollingStrategy : IReadingModeStrategy
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    public void OnCurrentIndexChanged(LocalReader reader, int oldValue, int newValue)
    {
        reader.ScrollIntoCurrentPage(newValue);
        reader.CheckCanPage();
    }

    /// <inheritdoc />
    public void NextPage(LocalReader reader)
    {
        if (CanNextPage(reader)) reader.ScrollIntoCurrentPage(reader.CurrentIndex + 1);
    }

    /// <inheritdoc />
    public void PrevPage(LocalReader reader)
    {
        if (CanPrevPage(reader)) reader.ScrollIntoCurrentPage(reader.CurrentIndex - 1);
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