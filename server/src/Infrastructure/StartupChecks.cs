using Cinegram.Jellyfin;
using Cinegram.MediaFiles;

namespace Cinegram.Infrastructure;

/// <summary>
/// Warnings worth seeing right after a start: missing system tools and Jellyfin library paths
/// Cinegram cannot reach. Neither stops anything.
/// </summary>
public sealed class StartupChecks : SupervisedBackgroundService
{
    private readonly JellyfinSettings _jellyfin;
    private readonly LibraryPaths _library;

    public StartupChecks(JellyfinSettings jellyfin, LibraryPaths library)
    {
        _jellyfin = jellyfin;
        _library = library;
    }

    public override string Name => "Startup";

    protected override bool RunsUntilShutdown => false;

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        if (!DependencyChecker.CheckExecutables(out var missing))
        {
            Log.Warning($"[Startup] Required system dependencies are missing: {string.Join(", ", missing)}. Make sure they are installed and available in the PATH.");
        }

        await JellyfinPathAudit.RunAsync(_jellyfin, _library, stoppingToken);
    }
}
