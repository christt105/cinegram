namespace Cinegram.TelegramBot;

/// <summary>
/// Whether the bot is up. The services that need Telegram wait on it before they start, and the
/// HTTP endpoints that act through the bot check it instead of reaching for an instance that may
/// not exist yet.
/// </summary>
public sealed class TelegramStatus
{
    public enum State
    {
        Starting,
        Ready,
        Failed,
        Disabled,
    }

    private readonly TaskCompletionSource<bool> _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public State Current { get; private set; } = State.Starting;

    /// <summary>Why the bot is <see cref="State.Failed"/> or <see cref="State.Disabled"/>.</summary>
    public string? Problem { get; private set; }

    public bool IsReady => Current == State.Ready;

    public void MarkReady() => Settle(State.Ready, null);

    public void MarkFailed(string problem) => Settle(State.Failed, problem);

    public void MarkDisabled(string problem) => Settle(State.Disabled, problem);

    /// <summary>Waits until the bot has started (true) or will not start (false).</summary>
    public Task<bool> WaitUntilSettledAsync(CancellationToken ct) => _settled.Task.WaitAsync(ct);

    private void Settle(State state, string? problem)
    {
        Current = state;
        Problem = problem;
        _settled.TrySetResult(state == State.Ready);
    }
}
