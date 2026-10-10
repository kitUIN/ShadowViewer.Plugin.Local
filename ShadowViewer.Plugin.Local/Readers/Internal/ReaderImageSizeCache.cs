using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>持久保存本地原图尺寸；路径、长度和修改时间一致时无需重新读取图片属性。</summary>
internal sealed class ReaderImageSizeCache(string cachePath)
{
    private const int MaxEntries = 20000;
    private readonly object gate = new();
    private readonly SemaphoreSlim fileGate = new(1, 1);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    private bool loaded;
    private long revision;
    private long savedRevision;

    internal sealed record Entry(long Length, long LastWriteTicks, double Width, double Height)
    {
        public bool HasValidSize => Width > 0 && Height > 0 && double.IsFinite(Width) && double.IsFinite(Height);
    }

    private sealed record CacheFile(int Version, Dictionary<string, Entry> Entries);

    public async Task<Size> GetAsync(string path, Func<CancellationToken, Task<Size>> readSize,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await EnsureLoadedAsync(token).ConfigureAwait(false);
        string fullPath = Path.GetFullPath(path);
        var stamp = GetStamp(fullPath);
        lock (gate)
        {
            if (stamp.HasValue && entries.TryGetValue(fullPath, out var entry) && entry.HasValidSize &&
                entry.Length == stamp.Value.Length && entry.LastWriteTicks == stamp.Value.Ticks)
                return new Size(entry.Width, entry.Height);
        }

        var size = await readSize(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        // 探测期间文件发生变化时不保存结果，下一次进入会重新验证。
        if (stamp.HasValue && stamp == GetStamp(fullPath) && size.Width > 0 && size.Height > 0 &&
            double.IsFinite(size.Width) && double.IsFinite(size.Height))
        {
            lock (gate)
            {
                entries[fullPath] = new Entry(stamp.Value.Length, stamp.Value.Ticks, size.Width, size.Height);
                while (entries.Count > MaxEntries) entries.Remove(entries.Keys.First());
                revision++;
            }
        }
        return size;
    }

    /// <summary>整章探测完成后统一落盘，避免每张图重写整个缓存。</summary>
    public async Task FlushAsync(CancellationToken token)
    {
        await EnsureLoadedAsync(token).ConfigureAwait(false);
        await fileGate.WaitAsync(token).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            CacheFile snapshot;
            long version;
            lock (gate)
            {
                if (revision == savedRevision) return;
                version = revision;
                snapshot = new CacheFile(1, new Dictionary<string, Entry>(entries, StringComparer.OrdinalIgnoreCase));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cachePath))!);
            temporary = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, cancellationToken: token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, cachePath, overwrite: true);
            temporary = null;
            lock (gate) savedRevision = version;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 缓存不可写时仍可正常阅读，下次有新尺寸时再尝试保存。
        }
        finally
        {
            if (temporary != null)
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            fileGate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken token)
    {
        await fileGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (loaded) return;
            try
            {
                await using var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    4096, FileOptions.Asynchronous);
                var cache = await JsonSerializer.DeserializeAsync<CacheFile>(stream, cancellationToken: token).ConfigureAwait(false);
                if (cache is { Version: 1, Entries: not null })
                {
                    lock (gate)
                    {
                        foreach (var pair in cache.Entries.TakeLast(MaxEntries))
                            if (pair.Value is { HasValidSize: true }) entries[pair.Key] = pair.Value;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // 文件缺失或损坏时重新读取尺寸，后续原子替换缓存。
            }
            loaded = true;
        }
        finally { fileGate.Release(); }
    }

    private static (long Length, long Ticks)? GetStamp(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? (file.Length, file.LastWriteTimeUtc.Ticks) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
}
