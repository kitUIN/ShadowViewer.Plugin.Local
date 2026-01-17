using System;
using Windows.ApplicationModel.DataTransfer;
using DryIoc;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ShadowViewer.Plugin.Local.I18n;
using ShadowPluginLoader.WinUI;
using Windows.Storage.Pickers;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Sdk.Helpers;
using ShadowViewer.Plugin.Local.ViewModels;
using ShadowViewer.Sdk.Extensions;

namespace ShadowViewer.Plugin.Local.Pages;

/// <summary>
/// 漫画属性页
/// </summary>
public sealed partial class AttributesPage
{
    /// <summary>
    /// 
    /// </summary>
    public AttributesViewModel ViewModel { get; } = DiFactory.Services.Resolve<AttributesViewModel>();

    /// <summary>
    /// 
    /// </summary>
    public AttributesPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 导航进入页面
    /// </summary>
    /// <param name="e"></param>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        switch (e.Parameter)
        {
            case LocalComic comic:
                ViewModel.Init(comic.Id);
                break;
            case long id:
                ViewModel.Init(id);
                break;
        }
    }

    /// <summary>
    /// 点击图片
    /// </summary>
    private async void Image_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var file = await FileHelper.SelectFileAsync("ShadowViewer_PicImageTapped", PickerViewMode.Thumbnail,
            FileHelper.Pngs);
        if (file == null) return;
        ViewModel.CurrentComic.Thumb = file.DecodePath();
    }

    /// <summary>
    /// 修改作者
    /// </summary>
    private async void AuthorButton_Click(object sender, RoutedEventArgs e)
    {
        // var dialog = XamlHelper.CreateOneTextBoxDialog( 
        //     ResourcesHelper.GetString(ResourceKey.Set),
        //     ResourcesHelper.GetString(ResourceKey.Author),
        //     "", 
        //     ViewModel.CurrentComic.Authors,
        //     (s, e, t) => { ViewModel.CurrentComic.Authors = t; });
        // await ViewModel.NotifyService.ShowDialog(this, dialog);
    }

    /// <summary>
    /// 修改漫画名称
    /// </summary>
    private async void FileNameButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = XamlHelper.CreateOneTextBoxDialog(
            I18N.Set,
            I18N.FileName,
            "", ViewModel.CurrentComic.Name,
            (_, _, t) => { ViewModel.CurrentComic.Name = t; });
        await ViewModel.NotifyService.ShowDialog(this, dialog);
    }

    /// <summary>
    /// 修改汉化组
    /// </summary>
    private async void GrouprButton_Click(object sender, RoutedEventArgs e)
    {
        // var dialog = XamlHelper.CreateOneTextBoxDialog(
        //     ResourcesHelper.GetString(ResourceKey.Set),
        //     ResourcesHelper.GetString(ResourceKey.Group),
        //     "", ViewModel.CurrentComic.,
        //     (s, e, t) => { ViewModel.CurrentComic.Group = t; });
        // await ViewModel.NotifyService.ShowDialog(this, dialog);
    }


    /// <summary>
    /// 浮出-删除
    /// </summary>
    private void RemoveTagButton_Click(object sender, RoutedEventArgs e)
    {
        // ViewModel.RemoveTag(TagId);
        // AddTagTeachingTip.Hide();
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