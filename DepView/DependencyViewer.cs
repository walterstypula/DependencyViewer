using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DepView
{
    public sealed class DependencyViewer
    {
        private readonly Dictionary<string, AssemblyInformation> _asmCollection = new();
        private readonly string _targetFile = string.Empty;
        public DependencyViewer(string root)
        {
            try
            {
                var attr = File.GetAttributes(root);
                var isDirectory = attr.HasFlag(FileAttributes.Directory);

                var directory = !isDirectory ? (new FileInfo(root)?.DirectoryName ?? root) : root;
                var assemblies = new List<string>();

                string[] dlls = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
                string[] exes = Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories);

                    assemblies.AddRange(dlls);
                    assemblies.AddRange(exes);


                foreach (var file in assemblies)
                    GatherInformation(file);

                if(!isDirectory)
                {
                    _targetFile = root;
                }


                var fileName = isDirectory 
                    ? null 
                    : _asmCollection.Values.First(a => $"{a.Location}{a.File}" == _targetFile);


#pragma warning disable CS8604 // Possible null reference argument.
                FindRelationships(fileName);
#pragma warning restore CS8604 // Possible null reference argument.
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new DependencyViewerException("One of the paths is inaccessable.", ex);
            }
            catch (Exception ex)
            {
                throw new DependencyViewerException("An unexpected exception was thrown.", ex);
            }
        }

        #region Methods

        public void WriteToStream(Stream stream)
        {
            using var writer = new StreamWriter(stream);

            if (_asmCollection.Count == 0)
            {
                writer.WriteLine("There are no assemblies to print.");
                return;
            }

            string[] headings = {
                "Assembly Name",
                "Ver Asm",
                "Ver File",
                "Ver Prod",
                "", // Architecture
                "Signed",
                "Resolved",
                "Possible Issue",
            };

            int[] widths = {
                _asmCollection.Values.Max(info => info.Name.Length) + 8, /* padding for indents */
                _asmCollection.Values.Max(info => info.VersionAsm.Length),
                _asmCollection.Values.Max(info => info.VersionFile.Length),
                _asmCollection.Values.Max(info => info.VersionProduct.Length),
                _asmCollection.Values.Max(info => info.Arch.Length),
                headings[5].Length,
                headings[6].Length,
                Math.Max(headings[7].Length, _asmCollection.Values.Max(info => info.ResolvedNote.Length))
            };

            PrintHorizontal(writer, widths);
            PrintRow(writer, headings, widths);
            PrintHorizontal(writer, widths);

            var filteredCollection = string.IsNullOrEmpty(_targetFile)
                                        ? _asmCollection.Values.Where(i => !i.ParentAssemblies.Any() || i.File.EndsWith("exe", StringComparison.InvariantCulture))
                                        : _asmCollection.Values.Where(i => i.AbsolutePath == _targetFile);


            foreach (var asm in filteredCollection)
                PrintAssembly(writer, asm, 0, widths);

            PrintHorizontal(writer, widths);
        }

        #endregion Methods

        #region Private methods

        private void GatherInformation(string file)
        {
            var info = new AssemblyInformation
            {
                AbsolutePath = file,
                Location = Path.GetDirectoryName(file) + Path.DirectorySeparatorChar,
                File = Path.GetFileName(file),
                VersionProduct = FileVersionInfo.GetVersionInfo(file).ProductVersion ?? string.Empty,
                VersionFile = FileVersionInfo.GetVersionInfo(file).FileVersion ?? string.Empty
            };

            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(file);

                info.DotNetAssembly = true;
                info.VersionAsm = assemblyName.Version?.ToString() ?? string.Empty;
                info.Name = assemblyName.Name ?? string.Empty;
                info.StronglySigned = assemblyName.GetPublicKeyToken()?.Length != 0;
                info.Arch = assemblyName.ProcessorArchitecture.ToString();

                var asm = Assembly.LoadFrom(file);
                info.ReferencedAssembliesRaw = asm.GetReferencedAssemblies();

                if (!_asmCollection.Keys.Contains(assemblyName.FullName))
                    _asmCollection.Add(assemblyName.FullName, info);
            }
            catch (FileLoadException)
            {
                var assemblyName = AssemblyName.GetAssemblyName(file);

                info.VersionAsm = assemblyName.Version?.ToString() ?? string.Empty;
                info.Name = assemblyName.Name ?? string.Empty;
                info.StronglySigned = assemblyName.GetPublicKeyToken()?.Length != 0;
                info.Arch = assemblyName.ProcessorArchitecture.ToString();
                info.ResolvedNote = "Unable to load";

                if (!_asmCollection.Keys.Contains(assemblyName.FullName))
                    _asmCollection.Add(assemblyName.FullName, info);
            }
            catch (BadImageFormatException)
            {
                info.DotNetAssembly = false;
                info.Name = Path.GetFileName(file);
                info.VersionAsm = string.Empty;

                if (!_asmCollection.Keys.Contains(file))
                    _asmCollection.Add(file, info);
            }
            catch (Exception)
            { }
        }

        private void FindRelationships(AssemblyInformation targetAssemblyInformation)
        {
            var filtered = targetAssemblyInformation != null
                ? _asmCollection.Values.Where(x => x == targetAssemblyInformation)
                : _asmCollection.Values;

            foreach (var asm in filtered)
            {
                if (asm.ReferencedAssembliesRaw == null || asm.ReferencedAssembliesRaw.Length == 0)
                    continue;

                asm.AllResolved = true;
                foreach (var refasm in asm.ReferencedAssembliesRaw)
                {
                    if (refasm.Name is null) continue;
                    if (refasm.Name == "mscorlib") continue;
                    if (refasm.Name == "WindowsBase") continue;
                    if (refasm.Name == "PresentationCore") continue;
                    if (refasm.Name == "PresentationFramework") continue;
                    if (refasm.Name.StartsWith("System", StringComparison.InvariantCulture)) continue;
                    if (refasm.Name.StartsWith("Microsoft", StringComparison.InvariantCulture)) continue;

                    byte[]? publicKeyToken = refasm.GetPublicKeyToken();
                    if (publicKeyToken != null && publicKeyToken.Length != 0)
                    {
                        var found = _asmCollection.Values.FirstOrDefault(i => i.Name == refasm.Name && i.VersionAsm == refasm.Version?.ToString());
                        if (found == null)
                        {
                            asm.AllResolved = false;
                            asm.ChildAssemblies.AddNotExists(new() { Name = refasm.Name, VersionAsm = refasm.Version?.ToString() ?? string.Empty });
                            continue;
                        }

                        asm.ChildAssemblies.AddNotExists(found);
                        found.ParentAssemblies.AddNotExists(asm);

                        FindRelationships(found);

                    }
                    else
                    {
                        var found = _asmCollection.Values.FirstOrDefault(i => i.Name == refasm.Name);
                        if (found == null)
                        {
                            asm.AllResolved = false;
                            asm.ChildAssemblies.AddNotExists(new() { Name = refasm.Name, VersionAsm = refasm.Version?.ToString() ?? string.Empty });
                            continue;
                        }

                        if (refasm.Version?.ToString() != found.VersionAsm)
                            asm.ResolvedNote += refasm.Version?.ToString() + " -> " + found.VersionAsm;

                        asm.ChildAssemblies.AddNotExists(found);
                        found.ParentAssemblies.AddNotExists(asm);
                        FindRelationships(found);
                    }


                }
            }
        }

        #endregion Private methods

        #region Print

        private void PrintAssembly(StreamWriter writer, AssemblyInformation asm, int level, int[] widths)
        {
            var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            if (asm.Location.StartsWith(runtimeDirectory, StringComparison.InvariantCulture))
                return;

            string name = "".PadRight(level) + asm.Name;
            if (level > 0)
            {
                name = " ".PadRight(level * "|  ".Length - 2, "|  ") + "\\-" + asm.Name;
            }

            string[] values = {
                name,
                asm.VersionAsm,
                asm.VersionFile,
                asm.VersionProduct,
                asm.Arch,
                asm.StronglySigned ? "Signed" : string.Empty,
                asm.DotNetAssembly ? (asm.AllResolved ? "Yes" : "No") : string.IsNullOrEmpty(asm.Location) ? string.Empty : "(N/A)",
                _asmCollection.Values.Where(i => i.Name == asm.Name).Count() > 1 ? "Dupe Asm" : string.IsNullOrEmpty(asm.Location) ? "Not Found" : asm.ResolvedNote,
            };

            PrintRow(writer, values, widths);

            foreach (var refasm in asm.ChildAssemblies)
                PrintAssembly(writer, refasm, level + 1, widths);
        }

        private static void PrintRow(StreamWriter writer, string[] headings, int[] widths)
        {
            string headers = string.Empty;
            for (int i = 0; i < widths.Length; i++)
                headers += "| " + headings[i].PadRight(widths[i] + 1).Substring(0, widths[i] + 1);
            headers += "|";
            writer.WriteLine(headers);
        }

        private static void PrintHorizontal(StreamWriter writer, int[] widths)
        {
            if (widths == null)
                return;

            string header = string.Empty;
            for (int i = 0; i < widths.Length; i++)
                header += "+".PadRight(widths[i] + 3, '-');
            header += "+";

            writer.WriteLine(header);
        }

        #endregion Print
    }

    public static class listextensions
    {
        internal static void AddNotExists(this List<AssemblyInformation> list, AssemblyInformation item)
        {
            if (list.Any(l=>l.Name == item.Name))
            {
                return;
            }

            list.Add(item);
        }
    }
}