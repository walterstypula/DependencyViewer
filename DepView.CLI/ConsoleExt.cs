using System;

namespace DepView.CLI
{
    internal static class ConsoleExt
    {
        public static void WriteError(string error)
        {
            WriteLine(error, ConsoleColor.Red);
        }

        public static void WriteError(string error, Exception ex)
        {
            WriteLine(error, ConsoleColor.Red);
            WriteLine(ex.ToString(), ConsoleColor.Gray);
        }

        public static void WriteLine(string value, ConsoleColor foreground)
        {
            var oldForeground = Console.ForegroundColor;
            Console.ForegroundColor = foreground;

            Console.WriteLine(value);

            Console.ForegroundColor = oldForeground;
        }
    }
}