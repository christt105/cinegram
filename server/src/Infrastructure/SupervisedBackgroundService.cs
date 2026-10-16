namespace Cinegram.Infrastructure;

/// <summary>
/// A background loop the host starts without waiting on it and that cannot take the host down.
/// <para>
/// On .NET 8 the host runs <see cref="BackgroundService.ExecuteAsync"/> synchronously up to its
/// first await, so a loop that starts with blocking work would hold back every service after it;
/// this yields first. An exception that ends the loop is logged here instead of stopping the host,
/// which would also stop the web, and kept in <see cref="Status"/> for the health report.
/// </para>
/// </summary>
public abstract class SupervisedBackgroundService : BackgroundService
{
    private volatile LoopStatus _status = new(LoopState.Waiting, null, DateTimeOffset.UtcNow);

    /// <summary>The tag in front of this service's log lines.</summary>
    public abstract string Name { get; }

    /// <summary>Where the loop is now, and why it ended if it ended badly.</summary>
    public LoopStatus Status => _status;

    /// <summary>False for work that is meant to finish, such as a one-off catch-up on startup.</summary>
    protected virtual bool RunsUntilShutdown => true;

    /// <summary>Waits for whatever the loop needs; false means it will never run.</summary>
    protected virtual Task<bool> WaitToStartAsync(CancellationToken stoppingToken) => Task.FromResult(true);

    protected abstract Task RunAsync(CancellationToken stoppingToken);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            if (!await WaitToStartAsync(stoppingToken))
            {
                SetStatus(LoopState.Skipped, null);
                return;
            }

            SetStatus(LoopState.Running, null);
            await RunAsync(stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                return;

            if (RunsUntilShutdown)
            {
                Log.Warning($"[{Name}] Stopped before shutdown.");
                SetStatus(LoopState.Stopped, "Stopped before shutdown");
            }
            else
            {
                SetStatus(LoopState.Done, null);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Error($"[{Name}] Stopped by an unexpected error", ex);
            SetStatus(LoopState.Crashed, ex.Message);
        }
    }

    private void SetStatus(LoopState state, string? problem) =>
        _status = new LoopStatus(state, problem, DateTimeOffset.UtcNow);
}

public enum LoopState
{
    /// <summary>Not started yet, or waiting for what it needs (the bot).</summary>
    Waiting,
    Running,
    /// <summary>One-off work that finished as meant.</summary>
    Done,
    /// <summary>Never ran because what it needs will not come up.</summary>
    Skipped,
    /// <summary>Returned on its own while it was meant to run until shutdown.</summary>
    Stopped,
    Crashed,
}

/// <param name="Since">When the loop entered <paramref name="State"/>.</param>
public sealed record LoopStatus(LoopState State, string? Problem, DateTimeOffset Since);
