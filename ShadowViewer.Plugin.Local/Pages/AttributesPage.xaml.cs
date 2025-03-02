using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using ShadowViewer.Plugin.Local.I18n;
using AttributesViewModel = ShadowViewer.Plugin.Local.ViewModels.AttributesViewModel;
using FluentIcon.WinUI;
using ShadowPluginLoader.WinUI;
using Windows.Storage.Pickers;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Core.Helpers;
using ShadowViewer.Core.Models;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 漫画属性页
/// </summary>
public sealed partial class AttributesPage : Page
{
    private AttributesViewModel ViewModel { get; } = DiFactory.Services.Resolve<AttributesViewModel>();
    private string TagId { get; set; }

    public AttributesPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is LocalComic comic) ViewModel.Init(comic.Id);
        else if (e.Parameter is long id) ViewModel.Init(id);
    }

    /// <summary>
    /// 点击图片
    /// </summary>
    private async void Image_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var file = await FileHelper.SelectFileAsync(XamlRoot, "ShadowViewer_PicImageTapped", PickerViewMode.Thumbnail, FileHelper.Pngs); ;
        // if (file != null) ViewModel.CurrentComic.Img = file.DecodePath();
    }

    /// <summary>
    /// 修改作者
    /// </summary>
    private async void AuthorButton_Click(object sender, RoutedEventArgs e)
    {
        // var dialog = XamlHelper.CreateOneTextBoxDialog(XamlRoot,
        //     ResourcesHelper.GetString(ResourceKey.Set),
        //     ResourcesHelper.GetString(ResourceKey.Author),
        //     "", 
        //     ViewModel.CurrentComic.Author,
        //     (s, e, t) => { ViewModel.CurrentComic.Author = t; });
        // await dialog.ShowAsync();
    }

    /// <summary>
    /// 修改漫画名称
    /// </summary>
    private async void FileNameButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = XamlHelper.CreateOneTextBoxDialog(XamlRoot,
            ResourcesHelper.GetString(ResourceKey.Set),
            ResourcesHelper.GetString(ResourceKey.FileName),
            "", ViewModel.CurrentComic.Name,
            (s, e, t) => { ViewModel.CurrentComic.Name = t; });
        await dialog.ShowAsync();
    }

    /// <summary>
    /// 修改汉化组
    /// </summary>
    private async void GrouprButton_Click(object sender, RoutedEventArgs e)
    {
        // var dialog = XamlHelper.CreateOneTextBoxDialog(XamlRoot,
        //     ResourcesHelper.GetString(ResourceKey.Set),
        //     ResourcesHelper.GetString(ResourceKey.Group),
        //     "", ViewModel.CurrentComic.Group,
        //     (s, e, t) => { ViewModel.CurrentComic.Group = t; });
        // await dialog.ShowAsync();
    }

    /// <summary>
    /// 点击标签
    /// </summary>
    private void Tag_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var tag = (ShadowTag)button.Tag;
        if (ViewModel.IsLastTag(tag))
        {
            TagName.Text = "";
            YesIcon.Symbol = FluentFilledIconSymbol.Tag24Filled;
            YesText.Text = ResourcesHelper.GetString(ResourceKey.AddNew);
            RemoveTagButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            BackgroundColorPicker.SelectedColor = ((SolidColorBrush)button.Background).Color;
            ForegroundColorPicker.SelectedColor = ((SolidColorBrush)button.Foreground).Color;
            TagName.Text = ((TextBlock)((StackPanel)button.Content).Children[1]).Text;
            YesIcon.Symbol = FluentFilledIconSymbol.TagReset24Filled;
            YesText.Text = ResourcesHelper.GetString(ResourceKey.Update);
            RemoveTagButton.Visibility = Visibility.Visible;
        }

        YesToolTip.Content = YesText.Text;
        // if (tag.IsEnable)
        // {
        //     TagId = tag.Id;
        //     TagSelectFlyout.ShowAt(sender as FrameworkElement);
        // }
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        // if (string.IsNullOrEmpty(TagName.Text)) return;
        // ViewModel.AddNewTag(new ShadowTag(TagName.Text, background: BackgroundColorPicker.SelectedColor,
        //     foreground: ForegroundColorPicker.SelectedColor) { Id = TagId });
        // TagSelectFlyout.Hide();
    }

    /// <summary>
    /// 浮出-删除
    /// </summary>
    private void RemoveTagButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveTag(TagId);
        TagSelectFlyout.Hide();
    }

    /// <summary>
    /// 控件初始化
    /// </summary>
    private void TopBorder_Loaded(object sender, RoutedEventArgs e)
    {
    }

    /// <summary>
    /// 跳转到看漫画
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Episode_Click(object sender, RoutedEventArgs e)
    {
    }

    private void TagName_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) Yes_Click(null, null);
    }

    private void IDButton_Click(object sender, RoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
    }

    /// <summary>
    /// 大小适应计算
    /// </summary>
    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = ((FrameworkElement)sender).ActualWidth;
        InfoStackPanel1.Width = width - 200;
        ViewModel.TextBlockMaxWidth = width - 330;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(((FrameworkElement)sender).Tag.ToString());
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
    }
}