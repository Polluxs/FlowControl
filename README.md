<div align="center">
  <img src="ForEach/logo.png" alt="ForEach Logo" width="150"/>
</div>

# ForEach

**Make .NET concurrency simple.**

Extension methods for parallel processing with fluent syntax. Built on `Parallel.ForEachAsync` with extras like result collection and per-key limits.

> **⚠️ Warning:** This package is in active development and may introduce breaking changes between versions.

## Quick Examples

### Parallel processing with concurrency limit

```csharp
// Process 100 URLs, max 20 at a time
await urls.ForEachParallelAsync(async (url, ct) =>
{
    var response = await httpClient.GetAsync(url, ct);
    Console.WriteLine($"{url}: {response.StatusCode}");
}, maxParallel: 20);
```

### Collect results from parallel operations

```csharp
// Download and collect all results
var results = await urls.ForEachParallelAsync(async (url, ct) =>
{
    var content = await httpClient.GetStringAsync(url, ct);
    return (url, content.Length);
}, maxParallel: 20);

foreach (var (url, size) in results)
    Console.WriteLine($"{url}: {size} bytes");
```

### Per-key concurrency limits

```csharp
// Max 50 total, but only 2 per customer
await orders.ForEachParallelByKeyAsync(
    keySelector: order => order.CustomerId,
    body: async (order, ct) => await ProcessOrderAsync(order, ct),
    maxTotalParallel: 50,
    maxPerKey: 2);
```

---

## All Methods

**For `IEnumerable<T>`, `IAsyncEnumerable<T>`, and `Channel<T>`:**

| Method | Purpose |
|:--|:--|
| [`ForEachParallelAsync`](#foreachparallelasync) | Process items concurrently with a global limit |
| [`ForEachParallelAsync<T,TResult>`](#foreachparallelasync-with-results) | Process items concurrently and collect results |
| [`ForEachParallelByKeyAsync`](#foreachparallelbykeyasync) | Process items with both global and per-key concurrency limits |

**For `Channel<T>` only:**

| Method | Purpose |
|:--|:--|
| [`ForEachAsync`](#foreachasync) | Process items sequentially |
| [`ReadAllAsync`](#readallasync) | Convert channel to `IAsyncEnumerable<T>` |
| [`WriteAllAsync`](#writeallasync) | Write items from `IEnumerable<T>` or `IAsyncEnumerable<T>` into channel |


---

## Cancellation Support

All methods support cancellation tokens at two levels:

**1. Method-level cancellation** (always available):
```csharp
var cts = new CancellationTokenSource();
await items.ForEachParallelAsync(async item =>
{
    await ProcessAsync(item);
}, maxParallel: 10, ct: cts.Token);  // ← Pass CT here
```

**2. Body-level cancellation** (optional - use when your work needs it):
```csharp
await items.ForEachParallelAsync(async (item, ct) =>  // ← CT parameter
{
    await ProcessAsync(item, ct);  // ← Pass to operations
}, maxParallel: 10);
```

**Don't need cancellation?** Just omit it:
```csharp
// Simplest form - no cancellation token needed
await items.ForEachParallelAsync(async item =>
{
    await ProcessAsync(item);
}, maxParallel: 10);
```

## Code examples for every method

### ForEachParallelAsync

Run async operations for an enumerable with a concurrency limit.

```csharp
using ForEach.Enumerable; // or ForEach.AsyncEnumerable

await files.ForEachParallelAsync(async (path, ct) =>
{
    var content = await File.ReadAllTextAsync(path, ct);
    var upper = content.ToUpperInvariant();
    await File.WriteAllTextAsync($"{path}.out", upper, ct);
}, maxParallel: 8);
```

- Global cap via `maxParallel`
- Honors cancellation
- Exception behavior:
  - `IEnumerable<T>`: Aggregates via `Parallel.ForEachAsync` → `AggregateException`
  - `IAsyncEnumerable<T>`: Aggregates via `Task.WhenAll` → `AggregateException`


### ForEachParallelAsync (with results)

Process items concurrently and collect results.

```csharp
var results = await urls.ForEachParallelAsync(async (url, ct) =>
{
    using var r = await httpClient.GetAsync(url, ct);
    return (url, r.StatusCode);
}, maxParallel: 16);

foreach (var (url, code) in results)
    Console.WriteLine($"{url} → {code}");
```

- Output order is arbitrary (not guaranteed)
- Works with any return type
- Uses `ConcurrentBag` under the hood
- Exception aggregation same as `ForEachParallelAsync` (inherits from `Parallel.ForEachAsync`)


### ForEachParallelByKeyAsync

Limit concurrency globally AND per key.

```csharp
await jobs.ForEachParallelByKeyAsync(
    keySelector: j => j.AccountId,
    body: async (job, ct) =>
    {
        await HandleJobAsync(job, ct);
    },
    maxTotalParallel: 64,
    maxPerKey: 2);
```

- Global cap = `maxTotalParallel` (actual items being processed concurrently)
- Per-key cap = `maxPerKey` (items per key being processed concurrently)
- Effective per-key limit = `min(maxTotalParallel, maxPerKey)` - if `maxPerKey > maxTotalParallel`, the global limit wins
- Uses bounded channel + per-key semaphores for efficient throttling
- Enumerates source only once (no materialization required)
- Aggregates exceptions via `Task.WhenAll` - multiple failures collected into an `AggregateException`


### ForEachAsync

Process channel items sequentially.

```csharp
await channel.ForEachAsync(async (item, ct) =>
    await ProcessAsync(item, ct));
```

### ReadAllAsync

Convert channel to `IAsyncEnumerable<T>`.

```csharp
await foreach (var item in channel.ReadAllAsync())
    Process(item);
```

### WriteAllAsync

Write items into channel.

```csharp
var channel = Channel.CreateUnbounded<int>();

// From IEnumerable<T>
await channel.WriteAllAsync(Enumerable.Range(1, 100));

// From IAsyncEnumerable<T>
await channel.WriteAllAsync(FetchDataAsync());

channel.Writer.Complete();
```

## License

MIT = copy, use, modify, ignore.
