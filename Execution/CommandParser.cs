using System;

namespace AScheduler.Execution;

/// <summary>
/// Parses command-line strings to extract executable file names and arguments.
/// Handles both quoted and unquoted file names with optional arguments.
/// </summary>
public static class CommandParser
{
    /// <summary>
    /// Parses a command string into file name and arguments components.
    /// </summary>
    /// <param name="command">The command string to parse. Must not be null or whitespace.</param>
    /// <returns>A tuple containing the file name and arguments components.</returns>
    /// <exception cref="ArgumentException">Thrown when command is null/whitespace or has invalid format.</exception>
    /// <remarks>
    /// Supports two formats:
    /// 1. Quoted: "C:\Program Files\App.exe" arg1 arg2
    /// 2. Unquoted: app.exe arg1 arg2
    /// </remarks>
    public static (string fileName, string arguments) Parse(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command is empty");

        command = command.Trim();

        if (command.StartsWith("\""))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote == -1)
                throw new ArgumentException("Invalid command format");

            var fileName = command.Substring(1, endQuote - 1);
            var arguments = command.Substring(endQuote + 1).Trim();

            return (fileName, arguments);
        }

        var firstSpace = command.IndexOf(' ');

        if (firstSpace < 0)
            return (command, "");

        var fileNamePart = command.Substring(0, firstSpace);
        var argumentsPart = command.Substring(firstSpace + 1).Trim();

        return (fileNamePart, argumentsPart);
    }
}
