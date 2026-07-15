using System.Diagnostics;
using System.IO;
using Bot.Models;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace Bot.Services;

public class ProgressStream : Stream
{
    private readonly Stream _baseStream;
    private readonly Action<long> _onProgress;
    private long _totalRead = 0;

    public ProgressStream(Stream baseStream, Action<long> onProgress)
    {
        _baseStream = baseStream;
        _onProgress = onProgress;
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _baseStream.Read(buffer, offset, count);
        if (read > 0)
        {
            _totalRead += read;
            _onProgress(_totalRead);
        }
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        if (read > 0)
        {
            _totalRead += read;
            _onProgress(_totalRead);
        }
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await _baseStream.ReadAsync(buffer, cancellationToken);
        if (read > 0)
        {
            _totalRead += read;
            _onProgress(_totalRead);
        }
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
    public override void SetLength(long value) => _baseStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);
}

public class UploadService
{
    private readonly WTelegram.Bot _bot;
    private readonly ApiClient _apiClient;
    private readonly TaskQueue _queue;
    private readonly int _allowedUser;

    public UploadService(WTelegram.Bot bot, ApiClient apiClient, TaskQueue queue)
    {
        _bot = bot;
        _apiClient = apiClient;
        _queue = queue;
        _allowedUser = Convert.ToInt32(Environment.GetEnvironmentVariable("TELEGRAM_AUTH_USER_ID"));
    }

    public async Task PollAndProcessAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingTasks = await _apiClient.GetPendingUploadsAsync();
                if (pendingTasks != null && pendingTasks.Count > 0)
                {
                    foreach (var task in pendingTasks)
                    {
                        Log.Info($"[Uploader] Found pending upload task {task.Id} for Jellyfin item {task.JellyfinId}");
                        // Mark as uploading immediately to avoid duplicate pickups
                        await _apiClient.UpdateUploadStatusAsync(task.Id, "uploading", 0);
                        
                        // Enqueue work
                        await _queue.Enqueue(() => ProcessUploadTask(task));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Uploader] Error in polling loop", ex);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessUploadTask(UploadTask task)
    {
        var tempDir = Path.Combine("/data/temp/uploads", task.Id.ToString());
        try
        {
            Log.Info($"[Uploader] Starting task {task.Id} ({task.Title})");
            Directory.CreateDirectory(tempDir);

            // 1. Translate host path to container path
            var localPath = TranslatePath(task.Path);
            Log.Info($"[Uploader] Translated path: {task.Path} -> {localPath}");

            if (!File.Exists(localPath) && !Directory.Exists(localPath))
            {
                throw new Exception($"Local file or directory not found: {localPath}");
            }

            // 2. Discover video files to upload
            var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" };
            var filesToUpload = new List<string>();

            if (File.Exists(localPath))
            {
                if (videoExtensions.Contains(Path.GetExtension(localPath).ToLowerInvariant()))
                {
                    filesToUpload.Add(localPath);
                }
            }
            else if (Directory.Exists(localPath))
            {
                var discovered = Directory.GetFiles(localPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();
                filesToUpload.AddRange(discovered);
            }

            if (filesToUpload.Count == 0)
            {
                throw new Exception("No video files found to upload.");
            }

            Log.Info($"[Uploader] Found {filesToUpload.Count} video file(s) to process.");

            // Calculate total size for progress reporting
            long totalBytesToUpload = 0;
            var fileSizes = new Dictionary<string, long>();
            foreach (var videoFile in filesToUpload)
            {
                var len = new FileInfo(videoFile).Length;
                totalBytesToUpload += len;
                fileSizes[videoFile] = len;
            }

            long totalUploadedBytes = 0;
            long lastReportedTime = DateTime.UtcNow.Ticks;

            // 3. Process and upload each video file
            for (int fileIndex = 0; fileIndex < filesToUpload.Count; fileIndex++)
            {
                var videoFile = filesToUpload[fileIndex];
                var fileInfo = new FileInfo(videoFile);
                var fileSize = fileInfo.Length;

                Log.Info($"[Uploader] Processing file {fileIndex + 1}/{filesToUpload.Count}: {fileInfo.Name} ({fileSize} bytes)");

                // Límite de 4GB
                const long splitLimit = 4000000000;

                if (fileSize > splitLimit)
                {
                    // Split file using 7z store-only (mx0)
                    Log.Info($"[Uploader] File is larger than 4GB. Splitting with 7z store-only...");
                    var partsDir = Path.Combine(tempDir, $"file_{fileIndex}");
                    Directory.CreateDirectory(partsDir);

                    var archiveBaseName = Path.GetFileNameWithoutExtension(videoFile) + ".zip";
                    var archivePath = Path.Combine(partsDir, archiveBaseName);

                    await SplitAndPackage(videoFile, archivePath);

                    var parts = Directory.GetFiles(partsDir, "*.*")
                        .OrderBy(f => f)
                        .ToList();

                    Log.Info($"[Uploader] Split complete. Created {parts.Count} parts.");

                    // Upload parts
                    for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                    {
                        var partPath = parts[partIndex];
                        var partInfo = new FileInfo(partPath);

                        Log.Info($"[Uploader] Uploading part {partIndex + 1}/{parts.Count}: {partInfo.Name}");
                        
                        await using (var fileStream = partInfo.OpenRead())
                        {
                            long lastBytes = 0;
                            var progressStream = new ProgressStream(fileStream, (transmitted) =>
                            {
                                var delta = transmitted - lastBytes;
                                lastBytes = transmitted;
                                Interlocked.Add(ref totalUploadedBytes, delta);

                                var nowTicks = DateTime.UtcNow.Ticks;
                                var elapsedSeconds = (nowTicks - lastReportedTime) / (double)TimeSpan.TicksPerSecond;

                                if (elapsedSeconds >= 3 || totalUploadedBytes == totalBytesToUpload)
                                {
                                    lastReportedTime = nowTicks;
                                    var percent = (int)(totalUploadedBytes * 100 / totalBytesToUpload);
                                    _ = _apiClient.UpdateUploadStatusAsync(task.Id, "uploading", percent);
                                }
                            });

                            var sent = await _bot.SendDocument(
                                _allowedUser,
                                new InputFileStream(progressStream, partInfo.Name),
                                caption: partInfo.Name
                            );

                            // Register part in DB
                            var uploadFile = new UploadFile
                            {
                                MessageId = sent.MessageId,
                                FileName = partInfo.Name,
                                FileSize = partInfo.Length,
                                MimeType = "application/zip",
                                UploadDate = DateTime.UtcNow.ToString("O"),
                                TmdbId = task.TmdbId
                            };
                            await _apiClient.UploadAsync(uploadFile);
                        }
                    }
                }
                else
                {
                    // Upload file directly
                    Log.Info($"[Uploader] File is under 4GB. Uploading directly...");
                    
                    await using (var fileStream = fileInfo.OpenRead())
                    {
                        long lastBytes = 0;
                        var progressStream = new ProgressStream(fileStream, (transmitted) =>
                        {
                            var delta = transmitted - lastBytes;
                            lastBytes = transmitted;
                            Interlocked.Add(ref totalUploadedBytes, delta);

                            var nowTicks = DateTime.UtcNow.Ticks;
                            var elapsedSeconds = (nowTicks - lastReportedTime) / (double)TimeSpan.TicksPerSecond;

                            if (elapsedSeconds >= 3 || totalUploadedBytes == totalBytesToUpload)
                            {
                                lastReportedTime = nowTicks;
                                var percent = (int)(totalUploadedBytes * 100 / totalBytesToUpload);
                                _ = _apiClient.UpdateUploadStatusAsync(task.Id, "uploading", percent);
                            }
                        });

                        var sent = await _bot.SendDocument(
                            _allowedUser,
                            new InputFileStream(progressStream, fileInfo.Name),
                            caption: fileInfo.Name
                        );

                        // Register in DB
                        var uploadFile = new UploadFile
                        {
                            MessageId = sent.MessageId,
                            FileName = fileInfo.Name,
                            FileSize = fileInfo.Length,
                            MimeType = GuessMime(videoFile),
                            UploadDate = DateTime.UtcNow.ToString("O"),
                            TmdbId = task.TmdbId
                        };
                        await _apiClient.UploadAsync(uploadFile);
                    }
                }
            }

            // 4. Update Status
            await _apiClient.UpdateUploadStatusAsync(task.Id, "completed", 100);
            Log.Info($"[Uploader] Upload task {task.Id} completed successfully.");
        }
        catch (Exception ex)
        {
            Log.Error($"[Uploader] Failed to process upload task {task.Id}", ex);
            await _apiClient.UpdateUploadStatusAsync(task.Id, "failed", 0, ex.Message);
        }
        finally
        {
            // 5. Cleanup temp folder
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Uploader] Failed to clean up temp folder: {tempDir}", ex);
            }
        }
    }

    private string TranslatePath(string hostPath)
    {
        if (hostPath.StartsWith("/mnt/disco/70-79_Media/Peliculas"))
        {
            return hostPath.Replace("/mnt/disco/70-79_Media/Peliculas", "/data/import/movies");
        }
        if (hostPath.StartsWith("/mnt/disco/70-79_Media/Series"))
        {
            return hostPath.Replace("/mnt/disco/70-79_Media/Series", "/data/import/shows");
        }
        return hostPath;
    }

    private static async Task SplitAndPackage(string filePath, string archivePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "7z",
            // mx0 = store only, v1900m = split in 1900MB parts
            Arguments = $"a -mx0 -v1900m \"{archivePath}\" \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);
        if (process == null) throw new Exception("Failed to start 7z process.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"7z splitting failed with exit code {process.ExitCode}: {error}");
        }
    }

    private static string GuessMime(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mkv" => "video/x-matroska",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".webm" => "video/webm",
            _ => "video/octet-stream"
        };
}
