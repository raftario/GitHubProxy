using System;

namespace GitHubProxy
{
    internal enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    internal static class Logger
    {
        private static void Log(object value, LogLevel level, bool nl)
        {
            var color = Console.ForegroundColor;
            Console.Write($"[{DateTime.Now:T} - ");
            Console.ForegroundColor = level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.Cyan,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
            };
            Console.Write(level);
            Console.ForegroundColor = color;
            Console.Write("] ");

            Console.Write(value);

            if (nl)
            {
                Console.WriteLine();
            }
        }

        public static void Debug(object value, bool nl = true)
        {
#if DEBUG
            Log(value, LogLevel.Debug, nl);
#endif
        }

        public static void Info(object value, bool nl = true) => Log(value, LogLevel.Info, nl);
        public static void Warning(object value, bool nl = true) => Log(value, LogLevel.Warning, nl);
        public static void Error(object value, bool nl = true) => Log(value, LogLevel.Error, nl);
    }
}
