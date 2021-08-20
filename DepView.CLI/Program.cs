﻿using CommandLine;
using System;
using System.IO;

namespace DepView.CLI
{
    public sealed class Options
    {
        [Option('f', "file", HelpText = "The root directory of the .NET assemblies to look through. If not specified, the current working directory is used.")]
        public string? File { get; set; }

        [Option('o', "output-file", HelpText = "A file to write the output to. If not specified, the output will be written to the console.")]
        public string? OutputFile { get; set; }
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


            var directoryPath = options.File ?? Environment.CurrentDirectory;
            var info = CreateViewer(directoryPath);
            if (info is null)
                return;

            if (options.OutputFile is null)
            {
                ConsoleExt.WriteLine($"Analyzis completed. Result:", ConsoleColor.Green);
                info.WriteToStream(Console.OpenStandardOutput());
            }
            else
            {
                using var fs = File.Create(options.OutputFile);
                info.WriteToStream(fs);

                ConsoleExt.WriteLine($"Analyzis completed. Result written to '{options.OutputFile}'.", ConsoleColor.Green);
            }
        }

        private static DependencyViewer? CreateViewer(string file)
        {
            try
            {
                return new(file);
            }
            catch (DependencyViewerException ex)
            {
                ConsoleExt.WriteError("The dependency loader threw an exception during initialization.", ex);
                return null;
            }
        }
    }
}