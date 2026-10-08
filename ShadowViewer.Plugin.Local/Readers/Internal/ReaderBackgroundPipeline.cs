using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>请求创建时的世代及取消令牌，章节切换会取消正在处理的旧请求。</summary>
internal readonly record struct PipelineRequest<TPayload>(TPayload Payload, int Epoch, CancellationToken CancellationToken = default);

/// <summary>有界且可重新启动的后台流水线；队满明确拒绝入队，避免请求被静默丢弃。</summary>
internal sealed class ReaderBackgroundPipeline<TPayload>
{
    private readonly Channel<PipelineRequest<TPayload>> channel;
    private readonly object gate = new();
    private readonly int workerCount;
    private readonly Func<PipelineRequest<TPayload>, CancellationToken, Task> processRequestAsync;
    private readonly Action<TPayload>? onDiscarded;
    private readonly Action<Exception>? onError;
    private CancellationTokenSource epochCts = new();
    private CancellationTokenSource? runCts;
    private Task[] workers = Array.Empty<Task>();
    private int epoch;
    private bool stopped;

    public ReaderBackgroundPipeline(int capacity, int workerCount, bool singleReader, bool singleWriter,
        Func<PipelineRequest<TPayload>, CancellationToken, Task> processRequestAsync,
        Action<TPayload>? onDiscarded = null, Action<Exception>? onError = null)
    {
        this.workerCount = workerCount;
        this.processRequestAsync = processRequestAsync;
        this.onDiscarded = onDiscarded;
        this.onError = onError;
        channel = Channel.CreateBounded<PipelineRequest<TPayload>>(new BoundedChannelOptions(capacity)
        {
            // Invalidate 会清理通道，Stop/Start 之间也可能短暂存在旧消费者。
            SingleReader = false,
            SingleWriter = singleWriter,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public void Start()
    {
        lock (gate)
        {
            if (runCts != null) return;
            stopped = false;
            runCts = new CancellationTokenSource();
            var token = runCts.Token;
            workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++) workers[i] = Task.Run(() => WorkerLoopAsync(token));
        }
    }

    public void Invalidate()
    {
        CancellationTokenSource previous;
        List<TPayload> discarded = new();
        lock (gate)
        {
            previous = epochCts;
            epochCts = new CancellationTokenSource();
            Interlocked.Increment(ref epoch);
            while (channel.Reader.TryRead(out var request)) discarded.Add(request.Payload);
        }
        previous.Cancel();
        previous.Dispose();
        foreach (var payload in discarded) onDiscarded?.Invoke(payload);
    }

    public bool TryEnqueue(TPayload payload)
    {
        lock (gate)
        {
            return !stopped && channel.Writer.TryWrite(new PipelineRequest<TPayload>(payload, epoch, epochCts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? previousRun;
        Task[] previousWorkers;
        lock (gate)
        {
            stopped = true;
            previousRun = runCts;
            runCts = null;
            previousWorkers = workers;
        }
        previousRun?.Cancel();
        Invalidate();
        if (previousRun != null)
            _ = Task.WhenAll(previousWorkers).ContinueWith(_ => previousRun.Dispose(), TaskScheduler.Default);
    }

    public bool IsCurrentEpoch(PipelineRequest<TPayload> request) => request.Epoch == Volatile.Read(ref epoch);
    public int CurrentEpoch => Volatile.Read(ref epoch);

    private async Task WorkerLoopAsync(CancellationToken runToken)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(runToken))
            {
                while (!runToken.IsCancellationRequested && channel.Reader.TryRead(out var request))
                {
                    if (!IsCurrentEpoch(request))
                    {
                        onDiscarded?.Invoke(request.Payload);
                        continue;
                    }
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(runToken, request.CancellationToken);
                    try { await processRequestAsync(request, linked.Token); }
                    catch (OperationCanceledException) when (linked.IsCancellationRequested)
                    {
                        onDiscarded?.Invoke(request.Payload);
                    }
                    catch (Exception ex)
                    {
                        onDiscarded?.Invoke(request.Payload);
                        onError?.Invoke(ex);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested) { }
    }
}
