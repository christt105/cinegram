using Telegram.Bot.Types.ReplyMarkups;
using TL;

namespace Bot.Handlers;

public class DownloadHandler
{
    private WTelegram.Bot _bot;
    
    private long _lastProgressBytes;
    private DateTime _lastProgressTime = DateTime.UtcNow;
    private DateTime _lastEditTime = DateTime.MinValue;
    
    // private async Task HandleVideoDownload(Message msg)
    // {
    //     var document = msg.Document;
    //     var fileName = document.FileName ?? $"file_{document.FileId}";
    //     var savePath = Path.Combine("/data/files", fileName);
    //     Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
    //
    //     await using var fileStream = File.Create(savePath);
    //
    //     // Recuperar la info completa del archivo
    //     var file = await _bot!.GetFile(document.FileId);
    //
    //     var downloadingMessage = await _bot.SendMessage(msg.Chat.Id, "Descargando", replyParameters: msg);
    //
    //     var tlMessage = msg.TLMessage() as TL.Message;
    //     var mmd = tlMessage.media as MessageMediaDocument;
    //     var doc = mmd.document as Document;
    //
    //     // Descargar con progreso usando Client directamente
    //     await _bot.Client.DownloadFileAsync(
    //         doc,
    //         fileStream,
    //         null,
    //         (transmitted, size) => DownloadProgress(transmitted, size, downloadingMessage)
    //     );
    //
    //     await _bot.SendMessage(msg.Chat, $"Archivo recibido y guardado como {fileName}");
    // }
    //
    // private async Task DownloadProgress(long transmitted, long size, Message message)
    // {
    //     try
    //     {
    //         var now = DateTime.UtcNow;
    //         var elapsedSeconds = (now - _lastProgressTime).TotalSeconds;
    //         if (elapsedSeconds <= 0) elapsedSeconds = 0.1; // evitar div/0
    //
    //         var delta = transmitted - _lastProgressBytes;
    //         var speed = delta / elapsedSeconds; // bytes/segundo
    //
    //         _lastProgressBytes = transmitted;
    //         _lastProgressTime = now;
    //
    //         // Calcular %
    //         var percent = (int)(transmitted * 100 / size);
    //
    //         
    //
    //         // Throttle → editar como máximo cada 1s
    //         if ((now - _lastEditTime).TotalSeconds >= 5 || transmitted == size)
    //         {
    //             _lastEditTime = now;
    //
    //             var progressText =
    //                 $"📥 Descargando...\n" +
    //                 $"Progreso: {percent}%\n" +
    //                 $"Transferido: {FormatSize(transmitted)} / {FormatSize(size)}\n" +
    //                 $"Velocidad: {FormatSize((long)speed)}/s";
    //
    //             await _bot!.EditMessageText(
    //                 message.Chat,
    //                 message.Id,
    //                 progressText,
    //                 replyMarkup: new InlineKeyboardMarkup(new[]
    //                 {
    //                     new[]
    //                     {
    //                         InlineKeyboardButton.WithCallbackData("❌ Cancelar", "cancelar")
    //                     }
    //                 })
    //             );
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine($"⚠️ Error en DownloadProgress: {e.Message}");
    //     }
    // }
}