namespace Bot;

public static class Log
{
    public static void Info(string message) =>
        Console.WriteLine($"[INFO {DateTime.Now:HH:mm:ss}] {message}");

    public static void Error(string message, Exception? ex = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR {DateTime.Now:HH:mm:ss}] {message}");
        if (ex != null) Console.WriteLine(ex);
        Console.ResetColor();
    }
}