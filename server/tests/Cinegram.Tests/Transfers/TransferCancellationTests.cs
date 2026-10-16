using System.Diagnostics;
using Xunit;
using Cinegram.Transfers;

namespace Cinegram.Tests.Transfers;

public class TransferCancellationTests
{
    [Fact]
    public void Cancelling_a_running_transfer_cancels_only_its_token()
    {
        var cancellation = new TransferCancellation();
        using var download = cancellation.Start(TransferKind.Download, 7);
        using var upload = cancellation.Start(TransferKind.Upload, 7);

        Assert.True(cancellation.Cancel(TransferKind.Download, 7));

        Assert.True(download.Token.IsCancellationRequested);
        Assert.False(upload.Token.IsCancellationRequested);
    }

    [Fact]
    public void A_finished_transfer_can_no_longer_be_cancelled()
    {
        var cancellation = new TransferCancellation();
        var running = cancellation.Start(TransferKind.Upload, 3);
        Assert.True(cancellation.IsRunning(TransferKind.Upload, 3));

        running.Dispose();

        Assert.False(cancellation.IsRunning(TransferKind.Upload, 3));
        Assert.False(cancellation.Cancel(TransferKind.Upload, 3));
        Assert.False(cancellation.Cancel(TransferKind.Download, 99));
    }

    [Fact]
    public async Task Cancelling_kills_the_external_tool()
    {
        using var cts = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        var run = SevenZip.RunAsync("sleep", "30", "test", cts.Token);

        await Task.Delay(200);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Took {clock.Elapsed}");
    }

    [Fact]
    public async Task A_failing_tool_reports_its_error_output()
    {
        var error = await Assert.ThrowsAsync<Exception>(() =>
            SevenZip.RunAsync("sh", "-c \"echo broken archive >&2; exit 2\"", "extraction", CancellationToken.None));

        Assert.Equal("sh extraction failed with exit code 2: broken archive\n", error.Message);
    }
}
