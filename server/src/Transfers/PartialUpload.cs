using Cinegram.Library;

namespace Cinegram.Transfers;

/// <summary>
/// The parts of one video file sent so far. The catalog only keeps files whole: when an upload
/// stops halfway through a file, cancelled or failed, the parts it already sent are deleted from
/// the chat and from the catalog, while the files it finished stay.
/// </summary>
public sealed class PartialUpload
{
    private readonly List<StoredUpload> _sent = [];
    private readonly List<int> _fileIds = [];

    public bool IsEmpty => _sent.Count == 0;

    public void Sent(StoredUpload stored) => _sent.Add(stored);

    public void Registered(int fileId) => _fileIds.Add(fileId);

    /// <summary>The file went up whole: its parts stay.</summary>
    public void Complete()
    {
        _sent.Clear();
        _fileIds.Clear();
    }

    /// <summary>Deletes what was sent of the file so far; each step runs even if the other fails.</summary>
    public async Task UndoAsync(Func<IReadOnlyList<StoredUpload>, Task> deleteMessages, Catalog catalog)
    {
        if (IsEmpty) return;

        var failures = new List<Exception>();
        try
        {
            await deleteMessages(_sent.ToList());
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        try
        {
            if (_fileIds.Count > 0)
                await catalog.DeleteFilesAsync(_fileIds.ToList());
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        Complete();
        if (failures.Count > 0)
            throw new AggregateException("Could not undo every part of the unfinished file", failures);
    }
}
