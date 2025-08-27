using Bot.Services;
using Microsoft.Data.Sqlite;

namespace Bot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var apiId = int.Parse(Environment.GetEnvironmentVariable("TELEGRAM_API_ID")!);
            var apiHash = Environment.GetEnvironmentVariable("TELEGRAM_API_HASH")!;
            var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")!;
            
            var backendUrl = Environment.GetEnvironmentVariable("BACKEND_URL") ?? "http:backend:8000";

            await using var connection = new SqliteConnection(@"Data Source=/data/bot.sqlite");
            using var apiClient = new ApiClient(backendUrl);

            var bot = new WTelegram.Bot(botToken, apiId, apiHash, connection);

            var botDispatcher = new BotDispatcher(bot, apiClient);

            await botDispatcher.InitBot();

            while (!stoppingToken.IsCancellationRequested) await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run the bot");
        }
    }
}