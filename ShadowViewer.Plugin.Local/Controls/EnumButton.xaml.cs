using System;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace ShadowViewer.Plugin.Local.Controls;

public sealed partial class EnumButton : UserControl
{
    public EnumButton()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(
            nameof(SelectedValue),
            typeof(object),
            typeof(EnumButton),
            new PropertyMetadata(null));

    public object SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public static readonly DependencyProperty TextConverterProperty =
        DependencyProperty.Register(
            nameof(TextConverter),
            typeof(IValueConverter),
            typeof(EnumButton),
            new PropertyMetadata(null));

    public IValueConverter TextConverter
    {
        get => (IValueConverter)GetValue(TextConverterProperty);
        set => SetValue(TextConverterProperty, value);
    }

    public static readonly DependencyProperty EnumSourceProperty =
        DependencyProperty.Register(
            nameof(EnumSource),
            typeof(Type),
            typeof(EnumButton),
            new PropertyMetadata(null));

    public Type EnumSource
    {
        get => (Type)GetValue(EnumSourceProperty);
        set => SetValue(EnumSourceProperty, value);
    }

    public static readonly DependencyProperty ButtonStyleProperty =
        DependencyProperty.Register(
            nameof(ButtonStyle),
            typeof(Style),
            typeof(EnumButton),
            new PropertyMetadata(null, OnButtonStyleChanged));

    public Style ButtonStyle
    {
        get => (Style)GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }

    private static void OnButtonStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EnumButton { InternalButton: { } button })
        {
            button.Style = (Style)e.NewValue;
        }
    }

    public Icon EnumIcon(object value)
    {
        var res = TextConverter?.Convert(value, typeof(Icon), null, null);
        if (res is Icon icon)
            return icon;
        return Icon.Note;
    }

    public IconVariant EnumIconVariant(object value)
    {
        var res = TextConverter?.Convert(value, typeof(Icon), null, null);
        if (res is IconVariant iconVariant)
            return iconVariant;
        return IconVariant.Regular;
    }


    public string EnumText(object value) =>
        (TextConverter?.Convert(value, typeof(string), null, null) ?? value)?.ToString() ?? string.Empty;
}