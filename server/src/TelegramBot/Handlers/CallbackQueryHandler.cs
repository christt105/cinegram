using System.Reflection;
using CallbackQuery = Telegram.Bot.Types.CallbackQuery;
using Cinegram.Infrastructure;
using Cinegram.TelegramBot.CallbackQueries;

namespace Cinegram.TelegramBot.Handlers;

public class CallbackQueryHandler
{
    private readonly Dictionary<string, Func<string[], ICallbackQuery>> _factories = new();
    private readonly List<string> _unbound = [];

    /// <summary>
    /// Finds every <see cref="CallbackAttribute"/> type and binds its static <c>Create(string[] fields, ...)</c>:
    /// the parameters after the fields are services, resolved once here. A callback whose services
    /// are not registered is left out and logged, rather than failing the first time it is pressed.
    /// </summary>
    public CallbackQueryHandler(IServiceProvider services)
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

            var parameters = createMethod.GetParameters();
            var dependencies = new object[parameters.Length - 1];
            var unresolved = new List<string>();
            for (var i = 1; i < parameters.Length; i++)
            {
                var dependency = services.GetService(parameters[i].ParameterType);
                if (dependency is null) unresolved.Add(parameters[i].ParameterType.Name);
                else dependencies[i - 1] = dependency;
            }

            if (unresolved.Count > 0)
            {
                _unbound.Add(attr.Id);
                Log.Error($"Callback {attr.Id} left out: {string.Join(", ", unresolved)} not registered.");
                continue;
            }

            _factories[attr.Id] = fields =>
                (ICallbackQuery)createMethod.Invoke(null, [fields, .. dependencies])!;
        }

        Log.Info($"Registered {_factories.Count} callback query handlers.\n\n{string.Join("\n", _factories.Keys)}");
    }

    /// <summary>The ids of the callbacks left out because a service they need is not registered.</summary>
    public IReadOnlyList<string> Unbound => _unbound;

    /// <summary>The ids of the callbacks that can be handled.</summary>
    public IReadOnlyCollection<string> Ids => _factories.Keys;

    /// <summary>The longest text Telegram accepts when answering a callback query.</summary>
    public const int NoticeLimit = 200;

    /// <summary>
    /// Runs the callback a button carries and returns the notice to answer it with: the callback's
    /// own notice, a hint when the button is no longer known, the error when it fails, or null.
    /// </summary>
    public async Task<string?> HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data is null)
        {
            Log.Info("CallbackQuery data is null");
            return null;
        }

        var data = callbackQuery.Data.Split(":");
        var id = data[0];
        var args = data[1..];

        if (!_factories.TryGetValue(id, out var factory))
        {
            Log.Error($"Callback query with id {id} not found");
            return "This button no longer works.";
        }

        try
        {
            var callback = factory(args);
            await callback.ExecuteAsync(callbackQuery.Message);
            return callback.Notice;
        }
        catch (Exception ex)
        {
            var error = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            Log.Error($"Error executing callback {id}: {error}");
            return Truncate($"❌ {error.Message}");
        }
    }

    private static string Truncate(string text) =>
        text.Length <= NoticeLimit ? text : text[..(NoticeLimit - 1)] + "…";
}
