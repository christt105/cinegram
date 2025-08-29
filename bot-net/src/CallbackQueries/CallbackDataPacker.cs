namespace Bot.CallbackQueries;

public static class CallbackDataPacker
{
    public static string Pack(string id, string[] fields)
    {
        return $"{id}:{string.Join(":", fields)}";
    }
}