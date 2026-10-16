using Cinegram.Infrastructure;
namespace Cinegram.Transfers;

/// <summary>
/// Transfer settings shared by every WTelegram client in the process.
/// </summary>
/// <remarks>
/// The library defaults are 2 parts of 512 KB in flight, which leaves the connection waiting on
/// round-trips rather than filling the link. Raising <see cref="ParallelTransfers"/> is safe to tune
/// per identity; raising the part size is not, and stays fixed at 512 KB for everyone.
///
/// 512 KB is the protocol's own maximum for uploads (upload.saveBigFilePart requires
/// 524288 % part_size == 0, i.e. part_size can only divide 512 KB), even though downloads
/// (upload.getFile) allow up to 1 MB. FilePartSize is one property shared by both directions on the
/// same WTelegram.Client, unvalidated by the library, so setting it above 512 KB broke every upload
/// through the account with a transport-level "Broken pipe" on the first request — confirmed by
/// reverting the part size alone back to 512 KB with everything else unchanged, which fixed it.
/// Concurrency was never the cause: the same failure reproduced at both 8 and 2 parallel transfers.
/// </remarks>
public sealed record TransferTuning(
    int? ParallelTransfersOverride,
    int FilePartSizeBytes,
    int PremiumConnections,
    long? UploadSplitLimitBytes)
{
    private const int MaxFilePartSizeBytes = 512 * 1024;

    /// <summary>Telegram's split size for a non-Premium account, used when the account is not in play.</summary>
    public const long StandardSplitLimitBytes = 1_950_000_000L;

    public const long PremiumSplitLimitBytes = 3_900_000_000L;

    public static TransferTuning Read(Func<string, string?> read) => new(
        Settings.PositiveInt(read, "TELEGRAM_PARALLEL_TRANSFERS"),
        Math.Min((Settings.PositiveInt(read, "TELEGRAM_FILE_PART_SIZE_KB") ?? 512) * 1024, MaxFilePartSizeBytes),
        PremiumDownloadTuning.ParseConnections(read(PremiumDownloadTuning.ConnectionsVariable)),
        Settings.Long(read, "UPLOAD_SPLIT_LIMIT_MB") is { } mb ? mb * 1_000_000L : null);

    public int ParallelTransfers(int defaultValue) => ParallelTransfersOverride ?? defaultValue;

    /// <summary><c>UPLOAD_SPLIT_LIMIT_MB</c> when set, else the limit of whoever sends the file.</summary>
    public long SplitLimitBytes(bool premiumAccount) =>
        UploadSplitLimitBytes ?? (premiumAccount ? PremiumSplitLimitBytes : StandardSplitLimitBytes);

    public void Apply(WTelegram.Client client, string who, int defaultParallelTransfers)
    {
        var parallelTransfers = ParallelTransfers(defaultParallelTransfers);
        client.FilePartSize = FilePartSizeBytes;
        client.ParallelTransfers = parallelTransfers;
        Log.Info($"[{who}] Transfers: {parallelTransfers} parts of {FilePartSizeBytes / 1024} KB in flight");
    }
}
