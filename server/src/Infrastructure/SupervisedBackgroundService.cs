namespace Cinegram.Infrastructure;

/// <summary>
/// A background loop the host starts without waiting on it and that cannot take the host down.
/// <para>
/// On .NET 8 the host runs <see cref="BackgroundService.ExecuteAsync"/> synchronously up to its
/// first await, so a loop that starts with blocking work would hold back every service after it;
/// this yields first. An exception that ends the loop is logged here instead of stopping the host,
/// which would also stop the web.
/// </para>
/// </summary>
public abstract class SupervisedBackgroundService : BackgroundService
{
    /// <summary>The tag in front of this service's log lines.</summary>
    public abstract string Name { get; }

    /// <summary>False for work that is meant to finish, such as a one-off catch-up on startup.</summary>
    protected virtual bool RunsUntilShutdown => true;

    protected abstract Task RunAsync(CancellationToken stoppingToken);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            await RunAsync(stoppingToken);

            if (RunsUntilShutdown && !stoppingToken.IsCancellationRequested)
                Log.Warning($"[{Name}] Stopped before shutdown.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Error($"[{Name}] Stopped by an unexpected error", ex);
        }
    }
}
