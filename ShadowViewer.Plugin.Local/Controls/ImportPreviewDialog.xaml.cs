using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ShadowViewer.Plugin.Local.ViewModels;

namespace ShadowViewer.Plugin.Local.Controls;

/// <summary>
/// 
/// </summary>
public sealed partial class ImportPreviewDialog
{
    /// <summary>
    /// 
    /// </summary>
    public ImportPreviewViewModel? ViewModel
    {
        get => (ImportPreviewViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ImportPreviewViewModel), typeof(ImportPreviewDialog),
            new PropertyMetadata(null));

    /// <summary>
    /// 
    /// </summary>
    public ImportPreviewDialog()
    {
        this.InitializeComponent();
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && sender is PasswordBox pb)
        {
            ViewModel.Password = pb.Password;
        }
    }
}