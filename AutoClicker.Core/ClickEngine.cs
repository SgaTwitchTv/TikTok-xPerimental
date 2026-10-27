namespace AutoClicker.Core;

public static class ClickTiming
{
    // Symmetric triangular distribution: mean = 1/cps, range = 70–130%.
    public static TimeSpan NextInterval(double cps, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (!double.IsFinite(cps) || cps < 2 || cps > 100)
            throw new ArgumentOutOfRangeException(nameof(cps));
        return TimeSpan.FromSeconds((1 + 0.3 * (random.NextDouble() + random.NextDouble() - 1)) / cps);
    }
}

public sealed class ClickEngine : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly Action click;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private CancellationTokenSource? cancellation;
    private Task worker = Task.CompletedTask;
    private double rate = 10;
    private bool disposed;

    public ClickEngine(Action click, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.click = click ?? throw new ArgumentNullException(nameof(click));
        this.delay = delay ?? ((interval, token) => Task.Delay(interval, token));
    }

    public double Rate
    {
        get => Volatile.Read(ref rate);
        set
        {
            if (!double.IsFinite(value) || value < 2 || value > 100)
                throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref rate, value);
        }
    }

    // The caller observes this task to report native input failures.
    public Task Start()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (cancellation is not null)
                return worker;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            worker = Task.Run(() => RunAsync(token));
            return worker;
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await delay(ClickTiming.NextInterval(Rate, Random.Shared), token).ConfigureAwait(false);
                lock (gate)
                {
                    token.ThrowIfCancellationRequested();
                    click();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async Task StopAsync()
    {
        Task pending;
        CancellationTokenSource? source;
        lock (gate)
        {
            source = cancellation;
            if (source is null) return;
            source.Cancel();
            pending = worker;
        }
        try { await pending.ConfigureAwait(false); }
        finally
        {
            lock (gate)
            {
                if (ReferenceEquals(cancellation, source))
                {
                    cancellation = null;
                    source?.Dispose();
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (gate) { disposed = true; }
        await StopAsync().ConfigureAwait(false);
    }
}
