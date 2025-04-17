using Microsoft.UI.Dispatching;
using Serilog;
using ShadowPluginLoader.Attributes;
using ShadowViewer.Core.Services;
using ShadowViewer.Plugin.Local.Models;
using ShadowViewer.Plugin.Local.Services.Interfaces;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SqlSugar;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace ShadowViewer.Plugin.Local.Services;

/// <summary>
/// Zip导出器
/// </summary>
public partial class ZipComicExporter : IComicExporter
{
    /// <inheritdoc />
    public bool Check(IStorageItem item)
    {
        return item is StorageFile file && SupportTypes.Contains(file.FileType);
    }

    /// <summary>
    /// NotifyService
    /// </summary>
    [Autowired]
    protected INotifyService NotifyService { get; }

    /// <summary>
    /// Logger
    /// </summary>
    [Autowired]
    protected ILogger Logger { get; }

    /// <summary>
    /// Db
    /// </summary>
    [Autowired]
    protected ISqlSugarClient Db { get; }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [Autowired]
    public string PluginId { get; }

    /// <inheritdoc />
    public virtual int Priority => 0;

    /// <inheritdoc />
    public virtual async Task ExportComic(StorageFile outputItem, LocalComic comic, string exportType,
        DispatcherQueue dispatcher,
        CancellationToken token)
    {
        if (exportType == ".zip")
        {
            using var archive = ZipArchive.Create();
            foreach (var ep in await Db.Queryable<LocalEpisode>().Where(x => x.ComicId == comic.Id).ToArrayAsync())
            {
                foreach (var pic in await Db.Queryable<LocalPicture>()
                             .Where(x => x.ComicId == comic.Id && x.EpisodeId == ep.Id).ToArrayAsync())
                {
                    archive.AddEntry($"{ep.Name}/{pic.Name}", pic.Img);
                }
            }

            archive.SaveTo(outputItem.Path, CompressionType.Deflate);
        }
    }

    /// <inheritdoc />
    public virtual string[] SupportTypes => [".zip"];
}