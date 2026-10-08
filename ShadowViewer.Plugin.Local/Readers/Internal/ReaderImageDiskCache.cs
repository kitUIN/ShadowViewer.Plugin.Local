using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>
/// 流式下载、验证和原子发布图片缓存。固定数量的锁避免按 URL 永久保存同步对象。
/// </summary>
internal sealed class ReaderImageDiskCache(
    HttpClient client,
    Func<string> getCacheDirectory,
    Func<string, CancellationToken, Task<Size?>> readSizeAsync)
{
    private static readonly SemaphoreSlim[] FileLocks = CreateLocks();

    internal readonly record struct CachedImage(string Path, Size Size);

    public async Task<CachedImage?> TryGetAsync(string url, CancellationToken cancellationToken)
    {
        string path = GetCachePath(url);
        var fileLock = GetFileLock(path);
        await fileLock.WaitAsync(cancellationToken);
        try { return await ReadValidImageAsync(path, cancellationToken); }
        finally { fileLock.Release(); }
    }

    public async Task<CachedImage> GetAsync(string url, CancellationToken cancellationToken)
    {
        string path = GetCachePath(url);
        var fileLock = GetFileLock(path);
        await fileLock.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            var cached = await ReadValidImageAsync(path, cancellationToken);
            if (cached.HasValue) return cached.Value;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            var downloaded = await ReadValidImageAsync(temporaryPath, cancellationToken);
            if (!downloaded.HasValue) throw new InvalidDataException("Downloaded content is not a valid image.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
            temporaryPath = null;
            return new CachedImage(path, downloaded.Value.Size);
        }
        finally
        {
            if (temporaryPath != null)
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            fileLock.Release();
        }
    }

    public async Task InvalidateAsync(string url, CancellationToken cancellationToken)
    {
        string path = GetCachePath(url);
        var fileLock = GetFileLock(path);
        await fileLock.WaitAsync(cancellationToken);
        try { File.Delete(path); }
        finally { fileLock.Release(); }
    }

    private async Task<CachedImage?> ReadValidImageAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0) return null;
            var size = await readSizeAsync(path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return size is { Width: > 0, Height: > 0 } &&
                   double.IsFinite(size.Value.Width) && double.IsFinite(size.Value.Height)
                ? new CachedImage(path, size.Value) : null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return null; } // 无效或不可读缓存由下一次下载替换。
    }

    private string GetCachePath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("An HTTP or HTTPS image URL is required.", nameof(url));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        // 保留已有缓存的文件名规则；有效性仍由内容解码器检查。
        string extension = Path.GetExtension(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 8 || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            extension = ".img";
        return Path.Combine(getCacheDirectory(), hash + extension);
    }

    private static SemaphoreSlim GetFileLock(string path) =>
        FileLocks[(uint)StringComparer.OrdinalIgnoreCase.GetHashCode(path) % (uint)FileLocks.Length];

    private static SemaphoreSlim[] CreateLocks()
    {
        var locks = new SemaphoreSlim[64];
        for (int i = 0; i < locks.Length; i++) locks[i] = new SemaphoreSlim(1, 1);
        return locks;
    }
}
