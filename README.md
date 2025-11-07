# ForEach

Extension methods that add parallel `ForEach` iterations to `IEnumerable<T>`, `IAsyncEnumerable<T>`, and `Channel<T>`.

Works on lists, arrays, collections, async streams, channels - anything that implements these interfaces.

.NET 8 only, use at own risk, it's not battle tested code.

**Quick examples:**

```csharp
using ForEach.Enumerable;

// Simple parallel processing - works on arrays, lists, any IEnumerable<T>
string[] files = Directory.GetFiles("/data");
await files.ForEachParallelAsync(async (file, ct) =>
{
    var content = await File.ReadAllTextAsync(file, ct);
    await ProcessAsync(content, ct);
}, maxParallel: 10);

// Parallel processing with results
string[] urls = ["https://api1.com", "https://api2.com", "https://api3.com"];
var results = await urls.ForEachParallelAsync(async (url, ct) =>
{
    var response = await httpClient.GetAsync(url, ct);
    return (url, response.StatusCode);
}, maxParallel: 5);

foreach (var (url, status) in results)
    Console.WriteLine($"{url}: {status}");

// Limit by key - max 100 total requests, max 2 per host
var requests = new[]
{
    new Request("https://api1.com/users"),
    new Request("https://api1.com/posts"),
    new Request("https://api2.com/data"),
    // ... hundreds more
};

await requests.ForEachParallelByKeyAsync(
    keySelector: req => req.Host,
    body: async (req, ct) =>
    {
        var response = await httpClient.GetAsync(req.Url, ct);
        Console.WriteLine($"{req.Url}: {response.StatusCode}");
    },
    maxTotalParallel: 100,
    maxPerKey: 2);

// Works on IEnumerable<T>, IAsyncEnumerable<T>, and Channel<T>
```

## ForEach Methods

The main parallel processing methods work on Enumerables, IAsync, and Channels:

### `IEnumerable<T>` (`using ForEach.Enumerable;`)
Works on: `List<T>`, `T[]` (arrays), `HashSet<T>`, `Dictionary<K,V>`, LINQ queries, or anything implementing `IEnumerable<T>`

### `IAsyncEnumerable<T>` (`using ForEach.AsyncEnumerable;`)
Works on: async LINQ, `await foreach` sources, database queries (EF Core), HTTP response streams, file I/O

### `Channel<T>` (`using ForEach.Channel;`)
Works on: `Channel<T>` for long-running pipelines, producer-consumer patterns, backpressure control

---

**All three types support these methods:**

| Method | Purpose |
|:--|:--|
| [`ForEachParallelAsync`](#foreachparallelasync) | Process items concurrently with a global limit |
| [`ForEachParallelAsync<T,TResult>`](#foreachparallelasyncttresult) | Process items concurrently and collect results |
| [`ForEachParallelByKeyAsync`](#foreachparallelbykeysync) | Process items with both global and per-key concurrency limits |

**`Channel<T>` also supports:**

| Method | Purpose |
|:--|:--|
| [`channel.ForEachAsync()`](#foreachasync---process-items-sequentially) | Process items sequentially (one at a time) |

---

## Additional Methods

### For `Channel<T>` only (`using ForEach.Channel;`)

These helper methods work directly on `Channel<T>` - no need to type `.Reader` or `.Writer`:

| Method | Purpose |
|:--|:--|
| [`channel.ReadAllAsync()`](#readallasync---read-items-as-async-stream) | Convert channel to `IAsyncEnumerable<T>` |
| [`channel.WriteAllAsync(source)`](#writeallasync---write-all-items-from-a-source) | Write all items from `IEnumerable<T>` or `IAsyncEnumerable<T>` into channel |


---
## In-depth: ForEach Methods
### `ForEachParallelAsync`

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


### `ForEachParallelAsync<T,TResult>`

Same, but returns results.

```csharp
var results = await urls.ForEachParallelAsync(async (url, ct) =>
{
    using var r = await http.GetAsync(url, ct);
    return (url, r.StatusCode);
}, maxParallel: 16);

foreach (var (url, code) in results)
    Console.WriteLine($"{url} → {code}");
```

- Output order is arbitrary (not guaranteed)
- Works with any return type
- Uses `ConcurrentBag` under the hood
- Exception aggregation same as `ForEachParallelAsync` (inherits from `Parallel.ForEachAsync`)


### `ForEachParallelByKeyAsync`

Limit concurrency globally AND per key (e.g., by user, account, or host).

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


---

## In-depth: Additional Methods

### Channel Helper Methods

#### ReadAllAsync() - Read items as async stream
```csharp
using ForEach.Channel;

// Without: verbose channel reader access
while (await channel.Reader.WaitToReadAsync())
while (channel.Reader.TryRead(out var item))
    Process(item);

// With: simple foreach
await foreach (var item in channel.ReadAllAsync())
    Process(item);
```

#### WriteAllAsync() - Write all items from a source
```csharp
var channel = Channel.CreateUnbounded<int>();

// From IEnumerable<T>
await channel.WriteAllAsync(Enumerable.Range(1, 100));

// From IAsyncEnumerable<T>
await channel.WriteAllAsync(FetchDataAsync());

// Note: You must manually complete the channel when done writing
channel.Writer.Complete();
```

#### ForEachAsync() - Process items sequentially
```csharp
// Process each item one at a time (sequential)
await channel.ForEachAsync(async (item, ct) =>
{
    // Items processed in order, one after another
    await ProcessAsync(item, ct);
});
```

---

## License

MIT = copy, use, modify, ignore.
