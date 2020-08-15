using CommandLine;

namespace DepView.CLI
{
    public sealed class Options
    {
        [Option('f', "file", Required = true, HelpText = "The root directory of the .NET assemblies to look through.")]
        public string File { get; set; } = string.Empty;
    }

    internal static class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(Run);
        }

        private static void Run(Options options)
        {
            var info = new DependencyViewer(options.File);
            info.DrawTable();
        }
    }
}