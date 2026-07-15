using System.Diagnostics;
using Bot.Models;
using TL;

namespace Bot.Services;

public class DownloadService
{
    private readonly WTelegram.Bot _bot;
    private readonly ApiClient _apiClient;
    private readonly TaskQueue _queue;
    private readonly int _allowedUser;

    public DownloadService(WTelegram.Bot bot, ApiClient apiClient, TaskQueue queue)
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
                var pendingTasks = await _apiClient.GetPendingDownloadsAsync();
                if (pendingTasks != null && pendingTasks.Count > 0)
                {
                    foreach (var task in pendingTasks)
                    {
                        Log.Info($"[Downloader] Found pending download task {task.TaskId} for collection {task.CollectionId}");
                        // Mark as downloading immediately to avoid duplicate pickups
                        await _apiClient.UpdateDownloadStatusAsync(task.TaskId, "downloading", 0);
                        
                        // Enqueue work
                        await _queue.Enqueue(() => ProcessDownloadTask(task));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Downloader] Error in polling loop", ex);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessDownloadTask(DownloadTask task)
    {
        var tempDir = Path.Combine("/data/temp/downloads", task.TaskId.ToString());
        try
        {
            Log.Info($"[Downloader] Starting task {task.TaskId} ({task.Title})");
            Directory.CreateDirectory(tempDir);

            // 1. Download all files
            var totalSize = task.Files.Sum(f => f.Filesize);
            long totalDownloaded = 0;
            long lastReportedTime = DateTime.UtcNow.Ticks;

            foreach (var file in task.Files)
            {
                Log.Info($"[Downloader] Fetching message {file.MessageId} for file {file.Filename}");
                var messages = await _bot.GetMessagesById(_allowedUser, new[] { file.MessageId });
                var msg = messages.FirstOrDefault();
                if (msg == null || msg.Document == null)
                    throw new Exception($"Message {file.MessageId} not found or has no document.");

                var tlMessage = msg.TLMessage as TL.Message;
                var mmd = tlMessage.media as MessageMediaDocument;
                var doc = mmd.document as TL.Document;

                var filePath = Path.Combine(tempDir, file.Filename);
                Log.Info($"[Downloader] Downloading {file.Filename} ({file.Filesize} bytes) to {filePath}");

                await using (var fileStream = System.IO.File.Create(filePath))
                {
                    long lastBytes = 0;
                    await _bot.Client.DownloadFileAsync(doc, fileStream, null, (transmitted, size) =>
                    {
                        var delta = transmitted - lastBytes;
                        lastBytes = transmitted;
                        Interlocked.Add(ref totalDownloaded, delta);

                        var nowTicks = DateTime.UtcNow.Ticks;
                        var elapsedSeconds = (nowTicks - lastReportedTime) / (double)TimeSpan.TicksPerSecond;

                        if (elapsedSeconds >= 3 || totalDownloaded == totalSize)
                        {
                            lastReportedTime = nowTicks;
                            var percent = (int)(totalDownloaded * 100 / totalSize);
                            _ = _apiClient.UpdateDownloadStatusAsync(task.TaskId, "downloading", percent);
                        }
                    });
                }
                Log.Info($"[Downloader] Finished downloading {file.Filename}");
            }

            // 2. Check and extract if it's an archive
            var archivePath = Directory.GetFiles(tempDir, "*.*")
                .FirstOrDefault(f => f.EndsWith(".7z.001") || f.EndsWith(".zip.001") || f.EndsWith(".zip") || f.EndsWith(".7z"));
            
            if (archivePath != null)
            {
                Log.Info($"[Downloader] Archive found: {archivePath}. Extracting...");
                var extractDir = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractDir);
                await ExtractArchive(archivePath, extractDir);
                Log.Info("[Downloader] Extraction complete.");
            }

            // 3. Find the main video file
            var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm" };
            var videoFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (videoFiles.Count == 0)
                throw new Exception("No video files found in downloaded files.");

            // Pick the largest video file as the main media file
            var mainVideo = videoFiles.OrderByDescending(f => new FileInfo(f).Length).First();
            var extension = Path.GetExtension(mainVideo);

            // 4. Construct Jellyfin naming paths
            var moviesDir = Environment.GetEnvironmentVariable("JELLYFIN_MOVIES_DIR") ?? "/data/jellyfin/movies";
            var showsDir = Environment.GetEnvironmentVariable("JELLYFIN_SHOWS_DIR") ?? "/data/jellyfin/shows";
            string fullPath;

            var qSuffix = !string.IsNullOrEmpty(task.Quality) ? $" - [{task.Quality}]" : "";

            if (task.MediaType == "movie")
            {
                string dirName;
                if (task.TmdbId != null)
                {
                    dirName = task.Year != null ? $"{task.Title} ({task.Year}) [tmdbid-{task.TmdbId}]" : $"{task.Title} [tmdbid-{task.TmdbId}]";
                }
                else
                {
                    dirName = task.Year != null ? $"{task.Title} ({task.Year})" : task.Title;
                }
                var fileName = task.Year != null ? $"{task.Title} ({task.Year}){qSuffix}{extension}" : $"{task.Title}{qSuffix}{extension}";
                fullPath = Path.Combine(moviesDir, dirName, fileName);
            }
            else
            {
                string dirName;
                if (task.TvdbId != null)
                {
                    dirName = $"{task.Title} [tvdbid-{task.TvdbId}]";
                }
                else
                {
                    dirName = task.Year != null ? $"{task.Title} ({task.Year})" : task.Title;
                }
                var seasonDir = $"Season {task.SeasonNumber:D2}";
                var fileName = task.Year != null 
                    ? $"{task.Title} ({task.Year}) - S{task.SeasonNumber:D2}E{task.EpisodeNumber:D2}{qSuffix}{extension}" 
                    : $"{task.Title} - S{task.SeasonNumber:D2}E{task.EpisodeNumber:D2}{qSuffix}{extension}";
                fullPath = Path.Combine(showsDir, dirName, seasonDir, fileName);
            }

            Log.Info($"[Downloader] Moving video to final path: {fullPath}");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            System.IO.File.Move(mainVideo, fullPath, overwrite: true);

            // 5. Update Status
            await _apiClient.UpdateDownloadStatusAsync(task.TaskId, "completed", 100);
            Log.Info($"[Downloader] Download task {task.TaskId} completed successfully.");
        }
        catch (Exception ex)
        {
            Log.Error($"[Downloader] Failed to process task {task.TaskId}", ex);
            await _apiClient.UpdateDownloadStatusAsync(task.TaskId, "failed", 0, ex.Message);
        }
        finally
        {
            // 6. Cleanup temp folder
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Downloader] Failed to clean up temp folder: {tempDir}", ex);
            }
        }
    }

    private static async Task ExtractArchive(string archivePath, string outputDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "7z",
            Arguments = $"x \"{archivePath}\" -o\"{outputDir}\" -y",
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
            throw new Exception($"7z extraction failed with exit code {process.ExitCode}: {error}");
        }
    }
}
