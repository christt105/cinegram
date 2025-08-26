using Microsoft.Data.Sqlite;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TL;
using WTelegram;
using Document = TL.Document;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;

namespace TelegramBot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private WTelegram.Bot? _bot;
    private long _lastProgressBytes;
    private DateTime _lastProgressTime = DateTime.UtcNow;
    private DateTime _lastEditTime = DateTime.MinValue;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Leer variables de entorno
            var apiId = int.Parse(Environment.GetEnvironmentVariable("TELEGRAM_API_ID")!);
            var apiHash = Environment.GetEnvironmentVariable("TELEGRAM_API_HASH")!;
            var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")!;

            using var connection = new SqliteConnection(@"Data Source=/data/bot.sqlite");

            _bot = new WTelegram.Bot(botToken, apiId, apiHash, connection);

            var me = await _bot.GetMe();
            _logger.LogInformation("Bot conectado como @{User}", me.Username);

            _bot.OnMessage += OnMessage;
            _bot.OnUpdate += OnUpdate;
            _bot.OnError += (ex, src) =>
            {
                _logger.LogError(ex, "Error en {Source}", src);
                return Task.CompletedTask;
            };

            await _bot.DropPendingUpdates();

            _logger.LogInformation("Esperando mensajes...");

            // Mantener corriendo hasta que se cancele
            while (!stoppingToken.IsCancellationRequested) await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar el bot");
        }
    }

    private async Task OnMessage(Message msg, UpdateType type)
    {
        if (msg.Document != null)
        {
            _bot.SendMessage(msg.Chat.Id, $"MessageId: {msg.Id}");
            _ = Task.Run(async () => await HandleVideoDownload(msg));
        }

        if (msg.Text == null) return;
        var text = msg.Text.ToLower();

        if (text == "/start")
        {
            await _bot!.SendMessage(msg.Chat, $"Hola, {msg.From}!");
        }
        else if (text == "/spiderman")
        {
            var messages = await _bot.GetMessagesById(msg.Chat.Id, new[] { 826 });
            var message = messages.FirstOrDefault();
            if (message != null) await HandleVideoDownload(message);
        }
        else if (text == "/hola")
        {
            await _bot!.SendMessage(msg.Chat.Id, "Hola mundo!", replyMarkup: new InlineKeyboardMarkup(new[]
            {
                new[] // Fila 1
                {
                    InlineKeyboardButton.WithCallbackData("🔍 Buscar", "buscar"),
                    InlineKeyboardButton.WithCallbackData("📂 Mis archivos", "archivos")
                },
                new[] // Fila 2
                {
                    InlineKeyboardButton.WithUrl("🌍 Visitar web", "https://example.com")
                }
            }));
        }
    }

    private async Task HandleVideoDownload(Message msg)
    {
        var document = msg.Document;
        var fileName = document.FileName ?? $"file_{document.FileId}";
        var savePath = Path.Combine("/data/files", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

        await using var fileStream = File.Create(savePath);

        // Recuperar la info completa del archivo
        var file = await _bot!.GetFile(document.FileId);

        var downloadingMessage = await _bot.SendMessage(msg.Chat.Id, "Descargando", replyParameters: msg);


        var tlMessage = msg.TLMessage() as TL.Message;
        var mmd = tlMessage.media as MessageMediaDocument;
        var doc = mmd.document as Document;

        // Descargar con progreso usando Client directamente
        await _bot.Client.DownloadFileAsync(
            doc,
            fileStream,
            null,
            (transmitted, size) => DownloadProgress(transmitted, size, downloadingMessage)
        );

        await _bot.SendMessage(msg.Chat, $"Archivo recibido y guardado como {fileName}");
    }

    private async Task DownloadProgress(long transmitted, long size, Message message)
    {
        try
        {
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - _lastProgressTime).TotalSeconds;
            if (elapsedSeconds <= 0) elapsedSeconds = 0.1; // evitar div/0

            var delta = transmitted - _lastProgressBytes;
            var speed = delta / elapsedSeconds; // bytes/segundo

            _lastProgressBytes = transmitted;
            _lastProgressTime = now;

            // Calcular %
            var percent = (int)(transmitted * 100 / size);

            string FormatSize(long bytes)
            {
                if (bytes > 1024L * 1024 * 1024)
                    return $"{bytes / 1024f / 1024f / 1024f:0.00} GB";
                if (bytes > 1024L * 1024)
                    return $"{bytes / 1024f / 1024f:0.00} MB";
                if (bytes > 1024)
                    return $"{bytes / 1024f:0.00} KB";
                return $"{bytes} B";
            }

            // Throttle → editar como máximo cada 1s
            if ((now - _lastEditTime).TotalSeconds >= 5 || transmitted == size)
            {
                _lastEditTime = now;

                var progressText =
                    $"📥 Descargando...\n" +
                    $"Progreso: {percent}%\n" +
                    $"Transferido: {FormatSize(transmitted)} / {FormatSize(size)}\n" +
                    $"Velocidad: {FormatSize((long)speed)}/s";

                await _bot!.EditMessageText(
                    message.Chat,
                    message.Id,
                    progressText,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("❌ Cancelar", "cancelar")
                        }
                    })
                );
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"⚠️ Error en DownloadProgress: {e.Message}");
        }
    }

    private Task OnUpdate(Update update)
    {
        if (update.Type == UpdateType.Unknown)
            _logger.LogInformation("Update desconocido: {Update}", update.TLUpdate?.GetType().Name);

        _logger.LogInformation(update.Type.ToString());

        return Task.CompletedTask;
    }
}