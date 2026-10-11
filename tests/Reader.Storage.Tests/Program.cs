using System.IO.Compression;
using ShadowViewer.Plugin.Local.Models.Interfaces;
using ShadowViewer.Plugin.Local.Readers.ImageSourceStrategies;
using Windows.Storage;

var directory = Path.Combine(Path.GetTempPath(), "ShadowViewer-Reader-Storage-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
int passed = 0, failed = 0;
var strategy = new StorageStrategy();
var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
try
{
    // Reproduce ZIP preview keys and the paths already persisted by the importer.
    var archivePath = Path.Combine(directory, "comic.zip");
    string[] keys = ["漫画/第 1 话/001 #%.png", "漫画/第 2 话/002.png", "cover.png"];
    using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
    {
        foreach (var key in keys)
        {
            using var stream = archive.CreateEntry(key).Open();
            stream.Write(bytes);
        }
    }
    var extracted = Path.Combine(directory, "extracted");
    ZipFile.ExtractToDirectory(archivePath, extracted);

    foreach (var key in keys)
    {
        var persistedPath = Path.Combine(extracted, key);
        await CheckAsync($"ZIP page opens through WinRT: {key}", async () =>
        {
            Require(File.Exists(persistedPath), "ZIP extraction did not produce the expected file.");
            var file = await strategy.ResolveAsync(new Picture { SourcePath = persistedPath });
            Require(file?.Path == Path.GetFullPath(persistedPath), "Reader resolved the wrong path.");
            var properties = await file!.Properties.GetImagePropertiesAsync();
            Require(properties.Width == 1 && properties.Height == 1, "Image properties could not be read.");
        });
    }

    var mixedPath = Path.Combine(extracted, keys[0]);
    await CheckAsync("String source preloads the extracted page bytes", async () =>
    {
        var context = new ImageLoadingContext { Source = mixedPath };
        await strategy.PreloadImageAsync(context);
        Require(context.Bytes?.SequenceEqual(bytes) == true, "Preloaded bytes do not match the archive page.");
    });
    await CheckAsync("Fully normalized paths still open", async () =>
    {
        var file = await strategy.ResolveAsync(Path.GetFullPath(mixedPath));
        Require(file != null, "Normalized path did not resolve.");
    });
    await CheckAsync("StorageFile sources are retained", async () =>
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(mixedPath));
        Require(ReferenceEquals(file, await strategy.ResolveAsync(file)), "StorageFile was replaced.");
    });
    await CheckAsync("Canceled requests do not open files", async () =>
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { await strategy.ResolveAsync(mixedPath, cancellation.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Canceled request succeeded.");
    });
    await CheckAsync("Actually missing files still report FileNotFoundException", async () =>
    {
        try { await strategy.ResolveAsync(Path.Combine(extracted, "漫画/missing.png")); }
        catch (FileNotFoundException) { return; }
        throw new InvalidOperationException("Missing file did not report a failure.");
    });
}
finally
{
    Directory.Delete(directory, recursive: true);
}
Console.WriteLine($"{passed}/{passed + failed} native storage checks passed.");
return failed == 0 ? 0 : 1;

async Task CheckAsync(string name, Func<Task> run)
{
    try { await run(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception exception) { failed++; Console.Error.WriteLine("FAIL " + name + "\n" + exception); }
}

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed class StorageStrategy : LocalFileStrategy
{
    public Task<StorageFile?> ResolveAsync(object source, CancellationToken token = default) =>
        GetStorageFile(source, token);
}

internal sealed class Picture : IUiPicture
{
    public int Index { get; set; }
    public string SourcePath { get; set; } = "";
}
