using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Bot.Utils;

/// <summary>
/// Provides utility methods to verify OS system dependencies.
/// </summary>
public static class DependencyChecker
{
    /// <summary>
    /// Checks if required system executables (7z, ffprobe) are available in the system PATH.
    /// </summary>
    /// <param name="missing">The list of missing executable names, if any.</param>
    /// <returns>True if all required executables are found; otherwise, false.</returns>
    public static bool CheckExecutables(out List<string> missing)
    {
        missing = new List<string>();
        var executables = new[] { "7z", "ffprobe" };

        foreach (var exec in executables)
        {
            if (!IsCommandAvailable(exec))
            {
                missing.Add(exec);
            }
        }

        return missing.Count == 0;
    }

    /// <summary>
    /// Checks if a specific command name exists in any directory listed in the PATH environment variable.
    /// </summary>
    /// <param name="command">The name of the command to look for.</param>
    /// <returns>True if the command is found in the PATH; otherwise, false.</returns>
    private static bool IsCommandAvailable(string command)
    {
        string cmdName = command;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cmdName += ".exe";
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return false;
        }

        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path, cmdName);
                if (File.Exists(fullPath))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore inaccessible paths or invalid characters in path variable
            }
        }

        return false;
    }
}
