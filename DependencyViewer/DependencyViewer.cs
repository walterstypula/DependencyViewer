using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DependencyViewer
{
    public class DependencyViewer
    {
        private readonly Dictionary<string, AssemblyInformation> _asmCollection = new Dictionary<string, AssemblyInformation>();

        public DependencyViewer(string root)
        {
            try
            {
                string[] dlls = Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories);
                string[] exes = Directory.GetFiles(root, "*.exe", SearchOption.AllDirectories);

                foreach (var file in dlls.Concat(exes))
                    GatherInformation(file);

                FindRelationships();
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("One of the paths is inaccessable: " + ex.Message);
            }
        }

        private void FindRelationships()
        {
            foreach (var asm in _asmCollection.Values)
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
                            asm.ChildAssemblies.Add(new AssemblyInformation() { Name = refasm.Name, VersionAsm = refasm.Version?.ToString() ?? string.Empty });
                            continue;
                        }

                        asm.ChildAssemblies.Add(found);
                        found.ParentAssemblies.Add(asm);
                    }
                    else
                    {
                        var found = _asmCollection.Values.FirstOrDefault(i => i.Name == refasm.Name);
                        if (found == null)
                        {
                            asm.AllResolved = false;
                            asm.ChildAssemblies.Add(new AssemblyInformation() { Name = refasm.Name, VersionAsm = refasm.Version?.ToString() ?? string.Empty });
                            continue;
                        }

                        if (refasm.Version?.ToString() != found.VersionAsm)
                            asm.ResolvedNote += refasm.Version?.ToString() + " -> " + found.VersionAsm;

                        asm.ChildAssemblies.Add(found);
                        found.ParentAssemblies.Add(asm);
                    }
                }
            }
        }

        public void DrawTable()
        {
            if (_asmCollection.Count == 0)
            {
                Console.WriteLine("There are no assemblies to print.");
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

            PrintHorizontal(widths);
            PrintRow(headings, widths);
            PrintHorizontal(widths);

            foreach (var asm in _asmCollection.Values.Where(i => !i.ParentAssemblies.Any() || i.File.EndsWith("exe", StringComparison.InvariantCulture)))
                PrintAssembly(asm, 0, widths);

            PrintHorizontal(widths);
        }

        private void PrintAssembly(AssemblyInformation asm, int level, int[] widths)
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

            PrintRow(values, widths);

            foreach (var refasm in asm.ChildAssemblies)
                PrintAssembly(refasm, level + 1, widths);
        }

        private static void PrintRow(string[] headings, int[] widths)
        {
            string headers = string.Empty;
            for (int i = 0; i < widths.Length; i++)
                headers += "| " + headings[i].PadRight(widths[i] + 1).Substring(0, widths[i] + 1);
            headers += "|";
            Console.WriteLine(headers);
        }

        public static void PrintHorizontal(int[] widths)
        {
            if (widths == null)
                return;

            string header = string.Empty;
            for (int i = 0; i < widths.Length; i++)
                header += "+".PadRight(widths[i] + 3, '-');
            header += "+";

            Console.WriteLine(header);
        }

        public void GatherInformation(string file)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine("File does not exist: " + file);
                return;
            }

            var info = new AssemblyInformation
            {
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
    }
}