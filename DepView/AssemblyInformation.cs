using System;
using System.Collections.Generic;
using System.Reflection;

namespace DepView
{
    /// <summary>
    /// Provides a collection of information that might be useful to know about a .NET assembly
    /// </summary>
    internal class AssemblyInformation
    {
        public string Name { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string VersionAsm { get; set; } = string.Empty;
        public string VersionFile { get; set; } = string.Empty;
        public string VersionProduct { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Arch { get; set; } = string.Empty;
        public bool DotNetAssembly { get; set; }
        public AssemblyName[] ReferencedAssembliesRaw { get; set; } = Array.Empty<AssemblyName>();
        public List<AssemblyInformation> ChildAssemblies { get; } = new();
        public List<AssemblyInformation> ParentAssemblies { get; } = new();
        public bool AllResolved { get; set; } = false;
        public bool StronglySigned { get; set; } = false;
        public string ResolvedNote { get; set; } = string.Empty;
        public string AbsolutePath { get; set; } = string.Empty;
    }
}