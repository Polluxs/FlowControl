# FlowControl

Helpers for running async work in parallel.
.NET 8 only, use at own risk, just sugar syntax for fast fun to get things done.

---

## Why This Exists

I love .NET to make things "quick and dirty" which is maybe ironic as .NET is typically known as a rather "enterprise" heavy language. Anyway from that perspective I love .NET to do things quick. Hence I am making a fun package for myself to build things "faster". This is what I am just "collecting" here. Feel free to take a look!

Also having fun with AI building this, so it might not be up to standards. I want to get an idea first of what I want and build it "quick" - planning to only fix things as I go. It's more for personal "get it done quick" than to share.

---

## 1-shot Concurrency

One-off parallel processing over enumerable collections. Run once, get results, done.

| Method | Purpose |
|:--|:--|
| [`ParallelAsync`](#parallelasync) | Run async work concurrently with a global cap |
| [`ParallelAsync<T,TResult>`](#parallelasyncttresult) | Run async work and collect results |
| [`ParallelAsyncByKey`](#parallelasyncbykey) | Run async work with per-key concurrency limits |

---

## `ParallelAsync`

Run async operations for an enumerable with a concurrency limit.

```csharp
await files.ParallelAsync(async (path, ct) =>
{
    var content = await File.ReadAllTextAsync(path, ct);
    var upper = content.ToUpperInvariant();
    await File.WriteAllTextAsync($"{path}.out", upper, ct);
}, maxParallel: 8);
```

- Global cap via `maxParallel`
- Honors cancellation
- Aggregates exceptions via `Parallel.ForEachAsync` - if multiple operations fail, they're collected into an `AggregateException`

---

## `ParallelAsync<T,TResult>`

Same, but returns results.

```csharp
var results = await urls.ParallelAsync(async (url, ct) =>
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
- Exception aggregation same as `ParallelAsync` (inherits from `Parallel.ForEachAsync`)

---

## `ParallelAsyncByKey`

Limit concurrency globally AND per key (e.g., by user, account, or host).

```csharp
await jobs.ParallelAsyncByKey(
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

## Continuous Concurrency

Ongoing stream processing with channels. Thinking about channel extensions to make life easier - work in progress.

---

## License

MIT — copy, use, modify, ignore.
