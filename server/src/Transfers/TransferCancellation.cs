using System.Collections.Concurrent;

namespace Cinegram.Transfers;

public enum TransferKind
{
    Download,
    Upload,
}

/// <summary>
/// One cancellation source per transfer being worked on, so cancelling a task from the web or the
/// bot also stops the work already in progress instead of only deleting its row.
/// </summary>
public sealed class TransferCancellation
{
    private readonly ConcurrentDictionary<(TransferKind, int), CancellationTokenSource> _running = new();

    /// <summary>Registers the transfer for the life of the returned handle.</summary>
    public Running Start(TransferKind kind, int taskId)
    {
        var source = new CancellationTokenSource();
        _running[(kind, taskId)] = source;
        return new Running(this, (kind, taskId), source);
    }

    /// <summary>False when the transfer is not being worked on.</summary>
    public bool Cancel(TransferKind kind, int taskId)
    {
        if (!_running.TryGetValue((kind, taskId), out var source))
            return false;

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        return true;
    }

    public bool IsRunning(TransferKind kind, int taskId) => _running.ContainsKey((kind, taskId));

    public sealed class Running : IDisposable
    {
        private readonly TransferCancellation _owner;
        private readonly (TransferKind, int) _key;
        private readonly CancellationTokenSource _source;

        internal Running(TransferCancellation owner, (TransferKind, int) key, CancellationTokenSource source)
        {
            _owner = owner;
            _key = key;
            _source = source;
        }

        public CancellationToken Token => _source.Token;

        public void Dispose()
        {
            _owner._running.TryRemove(new KeyValuePair<(TransferKind, int), CancellationTokenSource>(_key, _source));
            _source.Dispose();
        }
    }
}
