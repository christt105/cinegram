using System.Reflection;
using Bot.CallbackQueries;
using Bot.CallbackQueries.Callbacks;
using CallbackQuery = Telegram.Bot.Types.CallbackQuery;

namespace Bot.Handlers;

public class CallbackQueryHandler
{
    private readonly Dictionary<string, Func<string[], ICallbackQuery>> _factories = new();

    public CallbackQueryHandler(BotDispatcher bot)
    {
        var callbackTypes = typeof(CallbackQueryHandler).Assembly
            .GetTypes()
            .Where(t => typeof(ICallbackQuery).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in callbackTypes)
        {
            var attr = type.GetCustomAttribute<CallbackAttribute>();
            if (attr == null) continue;

            var createMethod = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null) continue;

            _factories[attr.Id] = fields =>
                (ICallbackQuery)createMethod.Invoke(null, [fields, bot])!;
        }

        Log.Info($"Registered {_factories.Count} callback query handlers.\n\n{string.Join("\n", _factories.Keys)}");
    }

    public async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data is null)
        {
            Log.Info("CallbackQuery data is null");
            return;
        }

        var data = callbackQuery.Data.Split(":");
        if (data.Length == 0)
        {
            Log.Info($"Failed to parse callback query data: {callbackQuery.Data}");
            return;
        }

        var id = data[0];
        var args = data[1..];

        if (!_factories.TryGetValue(id, out var factory))
        {
            Log.Error($"Callback query with id {id} not found");
            return;
        }

        var callback = factory(args);
        
        try
        {
            await callback.ExecuteAsync(callbackQuery.Message);
        }
        catch (Exception ex)
        {
            Log.Error($"Error executing callback {id}: {ex}");
        }
    }
}