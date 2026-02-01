using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage;

namespace ShadowViewer.Plugin.Local.ViewModels;

/// <summary>
/// 
/// </summary>
/// <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject" />
public partial class ImportPreviewViewModel : ObservableObject
{
    /// <summary>
    /// The item
    /// </summary>
    private readonly IStorageItem item;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    /// <value>
    /// The password.
    /// </value>
    [ObservableProperty]
    public partial string Password { get; set; } = "";

    /// <summary>
    /// Gets or sets the selected importer.
    /// </summary>
    /// <value>
    /// The selected importer.
    /// </value>
    [ObservableProperty]
    public partial IComicImporter? SelectedImporter { get; set; }

    /// <summary>
    /// Gets or sets the preview information.
    /// </summary>
    /// <value>
    /// The preview information.
    /// </value>
    [ObservableProperty]
    public partial ComicImportPreview? PreviewInfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is loading.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is loading; otherwise, <c>false</c>.
    /// </value>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// The import progress
    /// </summary>
    [ObservableProperty] public partial double ImportProgress { get; set; }

    /// <summary>
    /// The is importing
    /// </summary>
    [ObservableProperty] public partial bool IsImporting { get; set; }

    /// <summary>
    /// The preview cache
    /// </summary>
    private readonly Dictionary<IComicImporter, ComicImportPreview> previewCache = new();

    /// <summary>
    /// 导入器
    /// </summary>
    public ObservableCollection<IComicImporter> Importers { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportPreviewViewModel"/> class.
    /// </summary>
    public ImportPreviewViewModel(IStorageItem item, IEnumerable<IComicImporter> importers)
    {
        this.item = item;
        foreach (var imp in importers) Importers.Add(imp);
        SelectedImporter = Importers.FirstOrDefault();
    }

    partial void OnSelectedImporterChanged(IComicImporter? value)
    {
        Password = "";
        if (value != null)
            LoadPreview();
    }

    partial void OnPasswordChanged(string value)
    {
        if (SelectedImporter is ZipComicImporter zip)
        {
            zip.Password = value;
        }
    }

    /// <summary>
    /// Refreshes the preview.
    /// </summary>
    [RelayCommand]
    public void RefreshPreview()
    {
        if (SelectedImporter != null) previewCache.Remove(SelectedImporter);
        LoadPreview();
    }

    /// <summary>
    /// Loads the preview.
    /// </summary>
    private async void LoadPreview()
    {
        if (SelectedImporter == null) return;
        if (previewCache.TryGetValue(SelectedImporter, out var node))
        {
            PreviewInfo = node;
            return;
        }

        IsLoading = true;
        try
        {
            PreviewInfo = await SelectedImporter.Preview(item);
            if (PreviewInfo != null)
            {
                previewCache[SelectedImporter] = PreviewInfo;
            }
        }
        catch
        {
            PreviewInfo = null;
        }
        finally
        {
            IsLoading = false;
        }
    }
}