using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ShadowViewer.Plugin.Local.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Windows.Foundation;
using System.Threading.Tasks;
using Serilog;

namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 书架GridView
/// </summary>
public class BookShelfGridView : GridView
{
    /// <summary>
    /// Item双击Command
    /// </summary>
    public ICommand? ItemDoubleTappedCommand
    {
        get => (ICommand?)GetValue(ItemDoubleTappedCommandProperty);
        set => SetValue(ItemDoubleTappedCommandProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty ItemDoubleTappedCommandProperty =
        DependencyProperty.Register(nameof(ItemDoubleTappedCommand), typeof(ICommand), typeof(BookShelfGridView),
            new PropertyMetadata(null));

    #region ItemTemplate

    /// <summary>
    /// DetailItemTemplate 属性
    /// </summary>
    public DataTemplate? DetailItemTemplate
    {
        get => (DataTemplate)GetValue(DetailItemTemplateProperty);
        set => SetValue(DetailItemTemplateProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty DetailItemTemplateProperty =
        DependencyProperty.Register(nameof(DetailItemTemplate), typeof(DataTemplate), typeof(BookShelfGridView),
            new PropertyMetadata(null));

    /// <summary>
    /// SimpleItemTemplate 属性
    /// </summary>
    public DataTemplate? SimpleItemTemplate
    {
        get => (DataTemplate)GetValue(SimpleItemTemplateProperty);
        set => SetValue(SimpleItemTemplateProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty SimpleItemTemplateProperty =
        DependencyProperty.Register(nameof(SimpleItemTemplate), typeof(DataTemplate), typeof(BookShelfGridView),
            new PropertyMetadata(null));


    /// <summary>
    /// IsDetail 属性，指示是否是 DetailItemTemplate
    /// </summary>
    public bool IsDetail
    {
        get => (bool)GetValue(IsDetailProperty);
        set => SetValue(IsDetailProperty, value); // 私有 set 确保只由控件内部逻辑更新
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty IsDetailProperty =
        DependencyProperty.Register(nameof(IsDetail), typeof(bool), typeof(BookShelfGridView),
            new PropertyMetadata(false, OnIsDetailChanged));

    /// <summary>
    /// 响应切换
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnIsDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gridView = d as BookShelfGridView;
        if (gridView == null) return;

        var useDetailTemplate = (bool)e.NewValue;
        gridView.UpdateItemTemplate(useDetailTemplate);
    }

    #endregion

    #region 指示选中

    /// <summary>
    /// AnySelect 属性，指示 是否选中
    /// </summary>
    public bool AnySelect
    {
        get => (bool)GetValue(AnySelectProperty);
        private set => SetValue(AnySelectProperty, value); // 私有 set 确保只由控件内部逻辑更新
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty AnySelectProperty =
        DependencyProperty.Register(nameof(AnySelect), typeof(bool), typeof(BookShelfGridView),
            new PropertyMetadata(false));

    /// <summary>
    /// IsSingle 属性，指示 是否选中的单个
    /// </summary>
    public bool IsSingle
    {
        get => (bool)GetValue(IsSingleProperty);
        private set => SetValue(IsSingleProperty, value); // 私有 set 确保只由控件内部逻辑更新
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty IsSingleProperty =
        DependencyProperty.Register(nameof(IsSingle), typeof(bool), typeof(BookShelfGridView),
            new PropertyMetadata(false));

    /// <summary>
    /// IsMulti 属性，指示 是否选中的多个
    /// </summary>
    public bool IsMulti
    {
        get => (bool)GetValue(IsMultiProperty);
        private set => SetValue(IsMultiProperty, value); // 私有 set 确保只由控件内部逻辑更新
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty IsMultiProperty =
        DependencyProperty.Register(nameof(IsMulti), typeof(bool), typeof(BookShelfGridView),
            new PropertyMetadata(false));

    /// <summary>
    /// HasFolder 属性，指示 选中的中是否有Folder
    /// </summary>
    public bool HasFolder
    {
        get => (bool)GetValue(HasFolderProperty);
        private set => SetValue(HasFolderProperty, value); // 私有 set 确保只由控件内部逻辑更新
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty HasFolderProperty =
        DependencyProperty.Register(nameof(HasFolder), typeof(bool), typeof(BookShelfGridView),
            new PropertyMetadata(false));

    #endregion


    /// <summary>
    /// 
    /// </summary>
    public BookShelfGridView() : base()
    {
        DragItemsStarting += BookShelfGridView_DragItemsStarting;
        SelectionChanged += RightMenuSelectionChanged;
        DoubleTapped += OnItemDoubleTapped;
        // AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler(OnItemDoubleTapped), true);
        AddHandler(RightTappedEvent, new RightTappedEventHandler(MenuRightTapped), true);
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        UpdateItemTemplate(IsDetail);
    }

    private void UpdateItemTemplate(bool useDetailTemplate)
    {
        if (useDetailTemplate && DetailItemTemplate != null)
        {
            ItemTemplate = DetailItemTemplate;
        }
        else if (SimpleItemTemplate != null)
        {
            ItemTemplate = SimpleItemTemplate;
        }
    }

    /// <summary>
    /// 双击
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        try
        {
            // 这里有个bug会导致双击进入后直接崩溃
            // 目前猜测原因是有其他事件未完成,加个延迟等待
            await Task.Delay(100); 
            if (ItemDoubleTappedCommand == null) return;
            var doubleTappedItem = FindGridViewItemUnderPointer(e.OriginalSource as DependencyObject);
            if (doubleTappedItem == null) return;
            var item = ItemFromContainer(doubleTappedItem);
            if (!ItemDoubleTappedCommand.CanExecute(item)) return;
            ItemDoubleTappedCommand.Execute(item);
        }
        catch (Exception ex)
        {
            Log.Error("OnItemDoubleTapped: {e}", ex);
        }
    }

    /// <summary>
    /// 向上层查找到GridViewItem
    /// </summary>
    private GridViewItem? FindGridViewItemUnderPointer(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is GridViewItem item) return item;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }


    /// <summary>
    /// 拖动初始化,如果当前拖动的项目未选中,则进行选中
    /// </summary>
    private void BookShelfGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        foreach (var item in e.Items)
        {
            if (SelectedItems.Contains(item)) continue;
            var container = ContainerFromItem(item) as GridViewItem;
            if (container != null) container.IsSelected = true;
        }
    }

    /// <summary>
    /// 触控/鼠标-漫画项右键<br />
    /// 选中/显示悬浮菜单
    /// </summary>
    private void MenuRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var container = FindGridViewItemUnderPointer(e.OriginalSource as DependencyObject);
        if (container != null && container.IsSelected) return;
        foreach (var item in SelectedItems)
        {
            var innerContainer = ContainerFromItem(item) as GridViewItem;
            if (innerContainer != null) innerContainer.IsSelected = false;
        }
    }

    /// <summary>
    /// 选中响应更改 右键菜单显示
    /// </summary>
    private void RightMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AnySelect = SelectedItems.Count != 0;
        IsSingle = SelectedItems.Count == 1;
        IsMulti = SelectedItems.Count > 1;
        HasFolder = AnySelect && SelectedItems.Cast<LocalComic>().Any(item => item.IsFolder);
    }
}