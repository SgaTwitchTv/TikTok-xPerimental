using System.Threading.Channels;
using AutoClicker.Core;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

foreach (double cps in new[] { 2.0, 37.0, 100.0 })
{
    var random = new Random(12345);
    double sum = 0;
    const int count = 100_000;
    var distinct = new HashSet<long>();
    for (int i = 0; i < count; i++)
    {
        var interval = ClickTiming.NextInterval(cps, random);
        Check(interval.TotalSeconds >= 0.7 / cps - 0.0000001 &&
              interval.TotalSeconds <= 1.3 / cps + 0.0000001, "Interval out of bounds");
        sum += interval.TotalSeconds;
        distinct.Add(interval.Ticks);
    }
    Check(Math.Abs(sum / count * cps - 1) < 0.003, "Wrong mean");
    Check(distinct.Count > 1000, "Insufficient variation");
    Console.WriteLine($"PASS distribution {cps} CPS: mean {sum / count * 1000:F3} ms");
}
foreach (double invalid in new[] { 0, 1.9, 100.1, double.NaN, double.PositiveInfinity })
{
    try { ClickTiming.NextInterval(invalid, Random.Shared); throw new Exception("Invalid rate accepted"); }
    catch (ArgumentOutOfRangeException) { }
}

// Controlled scheduler: no native input, no timing-sensitive sleeps.
var requests = Channel.CreateUnbounded<(TimeSpan Interval, TaskCompletionSource Release)>();
int clicks = 0;
await using (var engine = new ClickEngine(() => Interlocked.Increment(ref clicks), async (interval, token) =>
{
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    await requests.Writer.WriteAsync((interval, release), token);
    await release.Task.WaitAsync(token);
}))
{
    engine.Rate = 2;
    Task firstWorker = engine.Start();
    Check(ReferenceEquals(firstWorker, engine.Start()), "Duplicate worker started");
    var first = await requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    Check(clicks == 0, "Click before initial wait");
    engine.Rate = 100;
    first.Release.SetResult();
    var next = await requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    Check(clicks == 1, "Click missing");
    Check(next.Interval.TotalMilliseconds is >= 7 and <= 13, "Live rate change ignored");
    await engine.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    next.Release.SetResult();
    Check(clicks == 1 && firstWorker.IsCompletedSuccessfully, "Click after cancellation");
    _ = engine.Start();
    await requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    await engine.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    Check(clicks == 1, "Restart cancellation failed");
}
Console.WriteLine("PASS live rate, duplicate start, stop during wait, restart, dispose");

var disposable = new ClickEngine(() => throw new Exception("Must not click"),
    (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
_ = disposable.Start();
await disposable.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
try { _ = disposable.Start(); throw new Exception("Start after disposal accepted"); }
catch (ObjectDisposedException) { }

var failing = new ClickEngine(() => throw new InvalidOperationException("Simulated input failure"),
    (_, _) => Task.CompletedTask);
try { await failing.Start().WaitAsync(TimeSpan.FromSeconds(5)); throw new Exception("Failure swallowed"); }
catch (InvalidOperationException) { }
try { await failing.StopAsync(); }
catch (InvalidOperationException) { }
await failing.DisposeAsync();
Console.WriteLine("PASS disposal and input failure propagation; no native clicks sent");
