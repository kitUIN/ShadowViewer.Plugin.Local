using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>
/// 后台流水线请求包装，包含有效负载与请求世代号。
/// </summary>
/// <typeparam name="TPayload">请求负载类型。</typeparam>
internal readonly struct PipelineRequest<TPayload>
{
    /// <summary>
    /// 初始化 <see cref="PipelineRequest{TPayload}"/> 的新实例。
    /// </summary>
    /// <param name="payload">请求负载。</param>
    /// <param name="epoch">请求创建时的世代号。</param>
    public PipelineRequest(TPayload payload, int epoch)
    {
        Payload = payload;
        Epoch = epoch;
    }

    /// <summary>
    /// 获取请求负载。
    /// </summary>
    public TPayload Payload { get; }

    /// <summary>
    /// 获取请求创建时的世代号。
    /// </summary>
    public int Epoch { get; }
}

/// <summary>
/// 通用后台流水线，提供有界通道、并发消费者、世代失效与统一取消。
/// </summary>
/// <typeparam name="TPayload">请求负载类型。</typeparam>
internal sealed class ReaderBackgroundPipeline<TPayload>
{
    private readonly Channel<PipelineRequest<TPayload>> channel;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly List<Task> workerTasks = new();
    private readonly int workerCount;
    private readonly Func<PipelineRequest<TPayload>, CancellationToken, Task> processRequestAsync;
    private int epoch;

    /// <summary>
    /// 初始化 <see cref="ReaderBackgroundPipeline{TPayload}"/> 的新实例。
    /// </summary>
    /// <param name="capacity">通道容量。</param>
    /// <param name="workerCount">消费者数量。</param>
    /// <param name="singleReader">是否单消费者读取。</param>
    /// <param name="singleWriter">是否单生产者写入。</param>
    /// <param name="processRequestAsync">请求处理函数。</param>
    public ReaderBackgroundPipeline(
        int capacity,
        int workerCount,
        bool singleReader,
        bool singleWriter,
        Func<PipelineRequest<TPayload>, CancellationToken, Task> processRequestAsync)
    {
        this.workerCount = workerCount;
        this.processRequestAsync = processRequestAsync;
        channel = Channel.CreateBounded<PipelineRequest<TPayload>>(new BoundedChannelOptions(capacity)
        {
            SingleReader = singleReader,
            SingleWriter = singleWriter,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <summary>
    /// 启动后台消费者。
    /// </summary>
    /// <returns>无返回值。</returns>
    public void Start()
    {
        lock (workerTasks)
        {
            if (workerTasks.Count > 0)
            {
                return;
            }

            for (int i = 0; i < workerCount; i++)
            {
                workerTasks.Add(Task.Run(() => WorkerLoopAsync(cancellationTokenSource.Token)));
            }
        }
    }

    /// <summary>
    /// 使当前流水线请求失效，之后仅处理新世代请求。
    /// </summary>
    /// <returns>无返回值。</returns>
    public void Invalidate()
    {
        Interlocked.Increment(ref epoch);

        // 失效时主动清掉积压请求，避免旧请求占用内存并在后续循环中白白消耗 CPU。
        while (channel.Reader.TryRead(out _))
        {
        }
    }

    /// <summary>
    /// 尝试将请求写入流水线。
    /// </summary>
    /// <param name="payload">请求负载。</param>
    /// <returns>写入成功返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public bool TryEnqueue(TPayload payload)
    {
        int currentEpoch = Volatile.Read(ref epoch);
        return channel.Writer.TryWrite(new PipelineRequest<TPayload>(payload, currentEpoch));
    }

    /// <summary>
    /// 停止后台消费者并完成通道。
    /// </summary>
    /// <returns>无返回值。</returns>
    public void Stop()
    {
        cancellationTokenSource.Cancel();
        channel.Writer.TryComplete();
    }

    /// <summary>
    /// 检查请求是否属于当前有效世代。
    /// </summary>
    /// <param name="request">待检查请求。</param>
    /// <returns>属于当前世代返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public bool IsCurrentEpoch(PipelineRequest<TPayload> request)
    {
        return request.Epoch == Volatile.Read(ref epoch);
    }

    /// <summary>
    /// 后台消费者循环。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示后台循环的异步操作。</returns>
    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (channel.Reader.TryRead(out var request))
                {
                    // 在消费端做世代过滤，可以确保旧请求不会回写新状态。
                    if (!IsCurrentEpoch(request))
                    {
                        continue;
                    }

                    await processRequestAsync(request, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 生命周期取消属于正常行为。
        }
    }
}
