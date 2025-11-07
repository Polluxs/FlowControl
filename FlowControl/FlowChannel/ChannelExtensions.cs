using System.Threading.Channels;
using FlowControl.FlowAsyncEnumerable;

namespace FlowControl.FlowChannel;

public static class ChannelExtensions
{
    public static async IAsyncEnumerable<T> ReadAllAsync<T>(
        this ChannelReader<T> reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            while (reader.TryRead(out var item))
                yield return item;
    }

    public static Task ForEachAsync<T>(
        this ChannelReader<T> reader,
        Func<T, CancellationToken, ValueTask> handler,
        CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            await foreach (var item in reader.ReadAllAsync(ct).ConfigureAwait(false))
                await handler(item, ct).ConfigureAwait(false);
        }, ct);
    }

    public static Task ParallelAsync<T>(
        this ChannelReader<T> reader,
        Func<T, CancellationToken, ValueTask> handler,
        int maxParallel = 32,
        CancellationToken ct = default)
    {
        return reader.ReadAllAsync(ct).ParallelAsync(handler, maxParallel, ct);
    }

    public static Task ParallelAsyncByKey<T, TKey>(
        this ChannelReader<T> reader,
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, ValueTask> handler,
        int maxParallel = 64,
        int maxPerKey = 4,
        CancellationToken ct = default)
        where TKey : notnull
    {
        return reader.ReadAllAsync(ct)
                     .ParallelAsyncByKey(keySelector, handler, maxParallel, maxPerKey, ct);
    }

    // Optional routing/tee
    public static async Task LinkTo<T>(
        this ChannelReader<T> reader,
        ChannelWriter<T> target,
        Func<T, bool>? predicate = null,
        CancellationToken ct = default)
    {
        try
        {
            await foreach (var item in reader.ReadAllAsync(ct).ConfigureAwait(false))
                if (predicate is null || predicate(item))
                    await target.WriteAsync(item, ct).ConfigureAwait(false);
        }
        finally
        {
            target.TryComplete();
        }
    }

    public static IAsyncEnumerable<T> ReadAllAsync<T>(
        this Channel<T> channel,
        CancellationToken ct = default)
        => channel.Reader.ReadAllAsync(ct);

    public static Task ForEachAsync<T>(
        this Channel<T> channel,
        Func<T, CancellationToken, ValueTask> handler,
        CancellationToken ct = default)
        => channel.Reader.ForEachAsync(handler, ct);

    public static Task ParallelAsync<T>(
        this Channel<T> channel,
        Func<T, CancellationToken, ValueTask> handler,
        int maxParallel = 32,
        CancellationToken ct = default)
        => channel.Reader.ParallelAsync(handler, maxParallel, ct);

    public static Task ParallelAsyncByKey<T, TKey>(
        this Channel<T> channel,
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, ValueTask> handler,
        int maxParallel = 64,
        int maxPerKey = 4,
        CancellationToken ct = default)
        where TKey : notnull
        => channel.Reader.ParallelAsyncByKey(keySelector, handler, maxParallel, maxPerKey, ct);

    public static Task LinkTo<T>(
        this Channel<T> channel,
        ChannelWriter<T> target,
        Func<T, bool>? predicate = null,
        CancellationToken ct = default)
        => channel.Reader.LinkTo(target, predicate, ct);

    public static Task WriteAllAsync<T>(
        this Channel<T> channel,
        IAsyncEnumerable<T> source,
        CancellationToken ct = default)
        => channel.Writer.WriteAllAsync(source, ct);

    public static Task WriteAllAsync<T>(
        this Channel<T> channel,
        IEnumerable<T> source,
        CancellationToken ct = default)
        => channel.Writer.WriteAllAsync(source, ct);

    public static bool TryComplete<T>(this Channel<T> channel, Exception? error = null)
        => channel.Writer.TryComplete(error);

    public static IDisposable CompleteOnDispose<T>(this Channel<T> channel, Exception? error = null)
        => channel.Writer.CompleteOnDispose(error);

    // === Writer ===

    public static async Task WriteAllAsync<T>(
        this ChannelWriter<T> writer,
        IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        try
        {
            await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                await writer.WriteAsync(item, ct).ConfigureAwait(false);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    public static async Task WriteAllAsync<T>(
        this ChannelWriter<T> writer,
        IEnumerable<T> source,
        CancellationToken ct = default)
    {
        try
        {
            foreach (var item in source)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteAsync(item, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    public static bool TryComplete<T>(this ChannelWriter<T> writer, Exception? error = null)
    {
        try { return writer.TryComplete(error); } catch { return false; }
    }

    public static IDisposable CompleteOnDispose<T>(this ChannelWriter<T> writer, Exception? error = null)
        => new CompleteOnDisposeImpl<T>(writer, error);

    private sealed class CompleteOnDisposeImpl<T> : IDisposable
    {
        private readonly ChannelWriter<T> _w; private readonly Exception? _e;
        public CompleteOnDisposeImpl(ChannelWriter<T> w, Exception? e) { _w = w; _e = e; }
        public void Dispose() => _w.TryComplete(_e);
    }

    // === Factory ===

    public static Channel<T> WithCapacity<T>(
        int capacity,
        BoundedChannelFullMode mode = BoundedChannelFullMode.Wait,
        bool singleReader = false,
        bool singleWriter = false)
        => System.Threading.Channels.Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = mode, SingleReader = singleReader, SingleWriter = singleWriter
        });
}
