using TL;

namespace Bot.Services;

/// <summary>
/// Wraps a WTelegram.Client user session alongside the bot.
/// Used for file transfers when the owner has Telegram Premium (4 GB limit, higher speed).
/// Auth is performed interactively via the /auth bot command.
/// </summary>
public class UserClientService : IDisposable
{
    private WTelegram.Client? _client;
    private Stream? _sessionStream;

    private int _apiId;
    private string _apiHash = "";
    private string _phoneNumber = "";

    // Auth flow signals
    private TaskCompletionSource<bool>? _codeRequested;
    private TaskCompletionSource<string>? _codeInput;
    private TaskCompletionSource<bool>? _passwordRequested;
    private TaskCompletionSource<string>? _passwordInput;
    private TaskCompletionSource<Exception?>? _loginFinished;

    private bool _resumeMode;

    public bool IsAuthenticated { get; private set; }
    public bool IsPremium { get; private set; }

    private const string SessionPath = "/data/user_client.session";

    public long SplitLimitBytes
    {
        get
        {
            var envMb = Environment.GetEnvironmentVariable("UPLOAD_SPLIT_LIMIT_MB");
            if (envMb != null && long.TryParse(envMb, out var mb))
                return mb * 1_000_000L;
            return IsAuthenticated && IsPremium ? 3_900_000_000L : 1_950_000_000L;
        }
    }

    public static long FallbackSplitLimitBytes
    {
        get
        {
            var envMb = Environment.GetEnvironmentVariable("UPLOAD_SPLIT_LIMIT_MB");
            if (envMb != null && long.TryParse(envMb, out var mb))
                return mb * 1_000_000L;
            return 1_950_000_000L;
        }
    }

    private string? ConfigFunc(string what) => what switch
    {
        "api_id" => _apiId.ToString(),
        "api_hash" => _apiHash,
        "phone_number" => _phoneNumber,
        "verification_code" => GetCode(),
        "password" => GetPassword(),
        _ => null
    };

    private string GetCode()
    {
        if (_resumeMode)
            throw new Exception("Session requires re-authentication; run /auth");

        _codeRequested?.TrySetResult(true);
        return _codeInput!.Task.GetAwaiter().GetResult();
    }

    private string GetPassword()
    {
        if (_resumeMode)
            throw new Exception("Session requires re-authentication; run /auth");

        _passwordRequested?.TrySetResult(true);
        return _passwordInput!.Task.GetAwaiter().GetResult();
    }

    private void DisposeClient()
    {
        _client?.Dispose();
        _sessionStream?.Dispose();
        _client = null;
        _sessionStream = null;
    }

    /// <summary>
    /// Tries to restore a saved session on startup. Returns false if no session exists
    /// or if it has expired (caller should prompt /auth).
    /// </summary>
    public async Task<bool> TryResumeSessionAsync(int apiId, string apiHash)
    {
        if (!File.Exists(SessionPath))
            return false;

        _apiId = apiId;
        _apiHash = apiHash;
        _resumeMode = true;

        _sessionStream = File.Open(SessionPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        _client = new WTelegram.Client(ConfigFunc, _sessionStream);

        try
        {
            var user = await _client.LoginUserIfNeeded();
            IsAuthenticated = true;
            IsPremium = (user.flags & User.Flags.premium) != 0;
            Log.Info($"[UserClient] Session restored. Premium={IsPremium}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Info($"[UserClient] Session resume failed: {ex.Message}");
            DisposeClient();
            return false;
        }
    }

    /// <summary>
    /// Starts an interactive login flow. Returns when Telegram has dispatched
    /// the verification code (i.e. the user can now call ProvideCodeAsync).
    /// </summary>
    public async Task BeginLoginAsync(int apiId, string apiHash, string phoneNumber)
    {
        DisposeClient();
        IsAuthenticated = false;
        IsPremium = false;

        _apiId = apiId;
        _apiHash = apiHash;
        _phoneNumber = phoneNumber;
        _resumeMode = false;

        _codeRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _codeInput = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _passwordRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _passwordInput = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loginFinished = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _sessionStream = File.Open(SessionPath, FileMode.Create, FileAccess.ReadWrite);
        _client = new WTelegram.Client(ConfigFunc, _sessionStream);

        _ = Task.Run(async () =>
        {
            try
            {
                var user = await _client.LoginUserIfNeeded();
                IsAuthenticated = true;
                IsPremium = (user.flags & User.Flags.premium) != 0;
                Log.Info($"[UserClient] Login complete. Premium={IsPremium}");
                _loginFinished.TrySetResult(null);
            }
            catch (Exception ex)
            {
                Log.Error("[UserClient] Login failed", ex);
                _loginFinished.TrySetResult(ex);
            }
        });

        await _codeRequested!.Task;
    }

    /// <summary>
    /// Submits the verification code.
    /// Returns true if a 2FA password is also needed, false if login is complete.
    /// Throws on auth failure.
    /// </summary>
    public async Task<bool> ProvideCodeAsync(string code, int timeoutSeconds = 60)
    {
        _codeInput!.TrySetResult(code);

        var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        await Task.WhenAny(_passwordRequested!.Task, _loginFinished!.Task, timeout);

        if (timeout.IsCompleted && !_loginFinished!.Task.IsCompleted && !_passwordRequested!.Task.IsCompleted)
            throw new TimeoutException("Timed out waiting for Telegram after submitting the code.");

        if (_loginFinished!.Task.IsCompleted)
        {
            var ex = await _loginFinished.Task;
            if (ex != null) throw ex;
            return false; // login complete, no 2FA
        }

        return true; // 2FA needed
    }

    /// <summary>
    /// Submits the 2FA cloud password. Throws on failure.
    /// </summary>
    public async Task ProvidePasswordAsync(string password, int timeoutSeconds = 60)
    {
        _passwordInput!.TrySetResult(password);

        var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        await Task.WhenAny(_loginFinished!.Task, timeout);

        if (timeout.IsCompleted && !_loginFinished!.Task.IsCompleted)
            throw new TimeoutException("Timed out waiting for Telegram after submitting the password.");

        var ex = await _loginFinished.Task;
        if (ex != null) throw ex;
    }

    // ── File operations ─────────────────────────────────────────────────────

    /// <summary>
    /// Uploads a file to Saved Messages. Returns the message ID.
    /// </summary>
    public async Task<int> SendDocumentToSavedAsync(
        string filePath,
        string fileName,
        string mimeType,
        Action<long, long>? onProgress = null)
    {
        EnsureReady();

        WTelegram.Client.ProgressCallback? cb = onProgress != null
            ? (transmitted, total) => onProgress(transmitted, total)
            : null;

        var inputFile = await _client!.UploadFileAsync(filePath, cb);
        var sent = await _client.SendMediaAsync(InputPeer.Self, fileName, inputFile, mimeType);
        return sent.id;
    }

    /// <summary>
    /// Fetches a document from Saved Messages by message ID.
    /// Returns null if the message doesn't exist or has no document.
    /// </summary>
    public async Task<Document?> GetDocumentFromSavedAsync(int messageId)
    {
        EnsureReady();

        var result = await _client!.GetMessages(
            InputPeer.Self,
            new InputMessage[] { new InputMessageID { id = messageId } }
        );

        var msg = result.Messages.OfType<Message>().FirstOrDefault();
        if (msg?.media is MessageMediaDocument { document: Document doc })
            return doc;

        return null;
    }

    /// <summary>
    /// Downloads a document to a stream.
    /// </summary>
    public async Task DownloadDocumentAsync(Document doc, Stream output, Action<long, long>? onProgress = null)
    {
        EnsureReady();

        WTelegram.Client.ProgressCallback? cb = onProgress != null
            ? (transmitted, total) => onProgress(transmitted, total)
            : null;

        await _client!.DownloadFileAsync(doc, output, null, cb);
    }

    private void EnsureReady()
    {
        if (!IsAuthenticated || _client == null)
            throw new InvalidOperationException("User client is not authenticated.");
    }

    public void Dispose() => DisposeClient();
}
