using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ShadowViewer.Plugin.Local.Enums;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;


namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 双页翻页阅读器的封装
/// </summary>
public sealed class MangaReader : Control
{
    /// <summary>
    /// 
    /// </summary>
    public MangaReader()
    {
        this.DefaultStyleKey = typeof(MangaReader);
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<IUiPicture> Pictures
    {
        get => (IList<IUiPicture>)GetValue(PicturesProperty);
        set
        {
            if (value is ObservableCollection<IUiPicture> pics)
            {
                pics.CollectionChanged -= Pics_CollectionChanged;
                pics.CollectionChanged += Pics_CollectionChanged;
            }

            SetValue(PicturesProperty, value);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty PicturesProperty =
        DependencyProperty.Register(nameof(Pictures), typeof(IList<IUiPicture>), typeof(MangaReader),
            new PropertyMetadata(new List<IUiPicture>(), OnCurrentIndexChanged));


    private void Pics_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateDisplayedPages();
    }

    /// <summary>
    /// 
    /// </summary>
    public int CurrentIndex
    {
        get => (int)GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CurrentIndexProperty =
        DependencyProperty.Register(nameof(CurrentIndex), typeof(int), typeof(MangaReader),
            new PropertyMetadata(0, OnCurrentIndexChanged));

    /// <summary>
    /// 
    /// </summary>
    public LocalReadMode ReadMode
    {
        get => (LocalReadMode)GetValue(ReadModeProperty);
        set => SetValue(ReadModeProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty ReadModeProperty =
        DependencyProperty.Register(nameof(ReadMode), typeof(LocalReadMode), typeof(MangaReader),
            new PropertyMetadata(LocalReadMode.TwoPageReadMode, OnReadModeChanged));

    /// <summary>
    /// 
    /// </summary>
    private static void OnReadModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MangaReader)d;
        var mode = (LocalReadMode)e.NewValue;
        control.Visibility = mode == LocalReadMode.TwoPageReadMode ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 
    /// </summary>
    private static void OnCurrentIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as MangaReader;
        control?.UpdateDisplayedPages();
    }

    /// <summary>
    /// 
    /// </summary>
    public ImageSource? CurrentLeftPage
    {
        get => (ImageSource?)GetValue(CurrentLeftPageProperty);
        private set => SetValue(CurrentLeftPageProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CurrentLeftPageProperty =
        DependencyProperty.Register(nameof(CurrentLeftPage), typeof(ImageSource), typeof(MangaReader),
            new PropertyMetadata(null));

    /// <summary>
    /// 
    /// </summary>
    public ImageSource? CurrentRightPage
    {
        get => (ImageSource?)GetValue(CurrentRightPageProperty);
        private set => SetValue(CurrentRightPageProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty CurrentRightPageProperty =
        DependencyProperty.Register(nameof(CurrentRightPage), typeof(ImageSource), typeof(MangaReader),
            new PropertyMetadata(null));

    /// <summary>
    /// 
    /// </summary>
    private void UpdateDisplayedPages()
    {
        if (CurrentIndex <= 0 || CurrentIndex > Pictures.Count) return;
        var index = CurrentIndex;
        if (index % 2 == 0) index -= 1;
        if (Pictures.Count >= index)
        {
            CurrentLeftPage = Pictures[index - 1].Source;
        }

        if (Pictures.Count >= index + 1)
        {
            CurrentRightPage = Pictures[index].Source;
        }
    }
}