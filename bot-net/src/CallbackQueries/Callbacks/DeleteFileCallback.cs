using Telegram.Bot.Types;

namespace Bot.CallbackQueries.Callbacks;

public class DeleteFileCallback : ICallbackQuery
{
    public const string Id = "delete-file";

    public Task ExecuteAsync(Message? message, CallbackQuery callbackQueryBase)
    {
        throw new NotImplementedException();
    }

    public static string Parse(int fileId)
    {
        return CallbackDataPacker.Pack(Id, [fileId.ToString()]);
    }
}