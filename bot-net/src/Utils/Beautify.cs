namespace Bot.Utils;

public static class Beautify
{
    public static string FormatSize(long? bytes)
    {
        return bytes switch
        {
            > 1024L * 1024 * 1024 => $"{bytes / 1024f / 1024f / 1024f:0.00} GB",
            > 1024L * 1024 => $"{bytes / 1024f / 1024f:0.00} MB",
            > 1024 => $"{bytes / 1024f:0.00} KB",
            _ => $"{bytes} B"
        };
    }
}