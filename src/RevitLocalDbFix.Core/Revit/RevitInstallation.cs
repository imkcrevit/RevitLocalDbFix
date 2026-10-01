namespace RevitLocalDbFix.Core.Revit
{
    /// <summary>One installed Revit version (SPEC §7).</summary>
    public sealed class RevitInstallation
    {
        public int Year { get; set; }
        public string ProductName { get; set; }
        /// <summary>Installation root with trailing backslash, read from the registry (SPEC §3.2).</summary>
        public string InstallPath { get; set; }
        public string ExePath { get; set; }
        public string ExeVersion { get; set; }
        /// <summary>&lt;InstallPath&gt;AddIns\SteelConnections — always derived from the real install path.</summary>
        public string SteelConnectionsPath { get; set; }
        public bool SteelConnectionsExists { get; set; }
        /// <summary>Newest "SteelConnections_disabled_*" folder if one exists, else null.</summary>
        public string DisabledSteelConnectionsPath { get; set; }
    }
}
