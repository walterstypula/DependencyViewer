using System;

namespace DepView
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Use: DepView <root path>");
                Console.WriteLine("    where <root Path> is the root directory of the .NET assemblies to look through");
            }

            if (args.Length >= 1)
            {
                var info = new DependencyViewer.DependencyViewer(args[0]);
                info.DrawTable();
            }
        }
    }
}