using System.Collections.Concurrent;
using Cinegram.Infrastructure;
using Cinegram.MediaFiles;

namespace Cinegram.Watch;

/// <summary>
/// Watches the downloads folder for new video files, waits for their size to stop growing,
/// and records them as watched files, notifying each new one on Telegram. Runs a reconciliation
/// sweep against the active rows on startup first, since FileSystemWatcher only sees events raised
/// while the process is running and guarantees nothing about downtime. The same sweep re-runs
/// periodically and on demand (the /rescan command, the web's "Scan folder" button), because
/// the watcher can also go quiet without notice while the process is still alive.
/// </summary>
public class WatchedFolderService
{
    private const int StabilitySamples = 3;
    private const int ReconciliationSamples = 2;
    private static readonly TimeSpan StabilityInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PeriodicSweepInterval = TimeSpan.FromMinutes(15);

    private readonly WatchedFiles _watchedFiles;
    private readonly WatchNotificationService? _notifications;
    private readonly ConcurrentDictionary<string, byte> _tracking = new();

    private readonly SemaphoreSlim _sweepGate = new(1, 1);

    private readonly string _root;
    private CancellationToken _lifetimeToken;

    public WatchedFolderService(WatchedFiles watchedFiles, LibraryPaths library,
        WatchNotificationService? notifications = null)
    {
        _watchedFiles = watchedFiles;
        _notifications = notifications;
        _root = library.DownloadsDir;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        _lifetimeToken = stoppingToken;

        try
        {
            Directory.CreateDirectory(_root);
        }
        catch (Exception ex)
        {
            Log.Error($"[WatchedFolder] Cannot access downloads directory {_root}", ex);
            return;
        }

        await RescanAsync(stoppingToken);

        using var watcher = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };
        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        watcher.Error += (_, e) => Log.Error("[WatchedFolder] FileSystemWatcher error", e.GetException());
        watcher.EnableRaisingEvents = true;

        Log.Info($"[WatchedFolder] Watching {_root} for new downloads.");

        try
        {
            await RunPeriodicSweepAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunPeriodicSweepAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(PeriodicSweepInterval, ct);

            try
            {
                await RescanAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error("[WatchedFolder] Periodic sweep failed", ex);
            }
        }
    }

    /// <summary>
    /// Diffs the downloads folder against the active rows, records whatever the watcher missed
    /// and retries the notification of rows still pending. Safe to call at any time: a file already being tracked is skipped, and
    /// concurrent calls queue behind each other.
    /// </summary>
    public async Task<WatchedFileReconciliation.ReconciliationDiff> RescanAsync(CancellationToken ct)
    {
        await _sweepGate.WaitAsync(ct);
        try
        {
            return await ReconcileAsync(ct);
        }
        finally
        {
            _sweepGate.Release();
        }
    }

    private async Task<WatchedFileReconciliation.ReconciliationDiff> ReconcileAsync(CancellationToken ct)
    {
        var onDiskRelative = MediaLibrary.EnumerateVideos(_root)
            .Select(path => WatchedFileReconciliation.ToRelativePath(_root, path))
            .ToList();

        var pending = await _watchedFiles.ListAsync("pending", ct);
        var notified = await _watchedFiles.ListAsync("notified", ct);
        var activeRelative = pending.Concat(notified).Select(w => w.Path);

        var diff = WatchedFileReconciliation.Compute(onDiskRelative, activeRelative);

        foreach (var relativePath in diff.Missing)
        {
            Log.Info($"[WatchedFolder] Reconciliation: {relativePath} is no longer on disk, marking missing.");
            await MarkMissingAsync(relativePath);
        }

        foreach (var relativePath in diff.New)
        {
            Log.Info($"[WatchedFolder] Reconciliation: {relativePath} has no watched row yet, verifying stability.");
            var fullPath = WatchedFileReconciliation.ToFullPath(_root, relativePath);
            await TrackAndReportAsync(fullPath, ReconciliationSamples, StabilityInterval, ct);
        }

        if (_notifications is not null) await _notifications.NotifyPendingAsync();

        return diff;
    }

    private void OnCreated(object? sender, FileSystemEventArgs e)
    {
        if (Directory.Exists(e.FullPath)) return;
        if (!MediaLibrary.IsVideo(e.FullPath)) return;

        _ = TrackAndReportAsync(e.FullPath, StabilitySamples, StabilityInterval, _lifetimeToken);
    }

    private void OnDeleted(object? sender, FileSystemEventArgs e)
    {
        if (!MediaLibrary.IsVideo(e.FullPath)) return;

        var relativePath = WatchedFileReconciliation.ToRelativePath(_root, e.FullPath);

        // A file Cinegram itself is moving into the library also disappears from here, which
        // looks identical to a real deletion by hand — see InFlightWatchedFileMoves.
        if (InFlightWatchedFileMoves.IsInFlight(relativePath))
        {
            Log.Info($"[WatchedFolder] {relativePath} disappeared because it's being moved by Cinegram itself, ignoring.");
            return;
        }

        _ = MarkMissingAsync(relativePath);
    }

    private void OnRenamed(object? sender, RenamedEventArgs e)
    {
        if (Directory.Exists(e.FullPath))
        {
            foreach (var newFilePath in MediaLibrary.EnumerateVideos(e.FullPath))
            {
                var oldFilePath = e.OldFullPath + newFilePath[e.FullPath.Length..];
                HandleRenamedFile(oldFilePath, newFilePath);
            }
            return;
        }

        HandleRenamedFile(e.OldFullPath, e.FullPath);
    }

    private void HandleRenamedFile(string oldPath, string newPath)
    {
        var action = WatchedFileReconciliation.DecideRenameAction(
            MediaLibrary.IsVideo(oldPath), MediaLibrary.IsVideo(newPath));

        switch (action)
        {
            case RenameAction.Ignore:
                break;

            case RenameAction.Rename:
                var oldRelative = WatchedFileReconciliation.ToRelativePath(_root, oldPath);
                var newRelative = WatchedFileReconciliation.ToRelativePath(_root, newPath);
                _ = RenameAsync(oldRelative, newRelative);
                break;

            case RenameAction.TrackNew:
                _ = TrackAndReportAsync(newPath, StabilitySamples, StabilityInterval, _lifetimeToken);
                break;

            case RenameAction.MarkMissing:
                var missingRelative = WatchedFileReconciliation.ToRelativePath(_root, oldPath);
                _ = MarkMissingAsync(missingRelative);
                break;
        }
    }

    private async Task TrackAndReportAsync(string fullPath, int requiredSamples, TimeSpan interval, CancellationToken ct)
    {
        if (!_tracking.TryAdd(fullPath, 0)) return;

        try
        {
            var size = await FileStabilityChecker.WaitForStableSizeAsync(
                () => TryGetFileSize(fullPath), requiredSamples, interval, ct);

            if (size is null) return;

            var relativePath = WatchedFileReconciliation.ToRelativePath(_root, fullPath);
            var filename = Path.GetFileName(fullPath);

            Log.Info($"[WatchedFolder] {relativePath} is stable at {size} bytes, recording.");
            var (row, created) = await _watchedFiles.ReportAsync(relativePath, filename, size.Value, ct);
            if (created && _notifications is not null) await _notifications.NotifyAsync(row.Id);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error($"[WatchedFolder] Failed to track {fullPath}", ex);
        }
        finally
        {
            _tracking.TryRemove(fullPath, out _);
        }
    }

    private async Task MarkMissingAsync(string relativePath)
    {
        try
        {
            var row = await _watchedFiles.MarkMissingAsync(relativePath);
            if (row is not null && _notifications is not null) await _notifications.NotifyRemovedAsync(row);
        }
        catch (Exception ex)
        {
            Log.Error($"[WatchedFolder] Failed to mark {relativePath} missing", ex);
        }
    }

    private async Task RenameAsync(string oldRelative, string newRelative)
    {
        try
        {
            await _watchedFiles.RenameAsync(oldRelative, newRelative);
        }
        catch (Exception ex)
        {
            Log.Error($"[WatchedFolder] Failed to follow the rename of {oldRelative}", ex);
        }
    }

    private static long? TryGetFileSize(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
