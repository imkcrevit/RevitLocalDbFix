using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RevitLocalDbFix.Core.Infrastructure;

namespace RevitLocalDbFix.Core.Revit
{
    /// <summary>
    /// Finds installed Revit versions from the registry (SPEC §3.2):
    /// primary HKLM\SOFTWARE\Autodesk\Revit\&lt;year&gt;\REVIT-xx:xxxx -> InstallationLocation
    /// [待实测 SPEC §13-1: value name across 2018-2026],
    /// fallback uninstall entries whose DisplayName is exactly "Revit &lt;year&gt;".
    /// A hit counts only when &lt;path&gt;\Revit.exe exists.
    /// </summary>
    public sealed class RevitInstallationLocator
    {
        public const int MinYear = 2018;
        public const int MaxYear = 2032;

        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        private readonly IRegistryReader _reg;
        private readonly IFileSystemProbe _fs;

        public RevitInstallationLocator() : this(new RegistryReader64(), new FileSystemProbe()) { }

        public RevitInstallationLocator(IRegistryReader reg, IFileSystemProbe fs)
        {
            _reg = reg ?? throw new ArgumentNullException("reg");
            _fs = fs ?? throw new ArgumentNullException("fs");
        }

        public IReadOnlyList<RevitInstallation> Enumerate()
        {
            var list = new List<RevitInstallation>();
            for (int year = MinYear; year <= MaxYear; year++)
            {
                var found = LocateYear(year);
                if (found != null) list.Add(found);
            }
            return list;
        }

        private RevitInstallation LocateYear(int year)
        {
            string root = @"SOFTWARE\Autodesk\Revit\" + year;
            foreach (var sub in _reg.GetSubKeyNames(root))
            {
                if (!sub.StartsWith("REVIT-", StringComparison.OrdinalIgnoreCase)) continue;
                string keyPath = root + "\\" + sub;
                var location = _reg.GetValue(keyPath, "InstallationLocation") as string;
                var productName = _reg.GetValue(keyPath, "ProductName") as string;
                var inst = Build(year, productName ?? "Revit " + year, location);
                if (inst != null) return inst;
            }

            foreach (var uninstallRoot in UninstallRoots)
            {
                foreach (var sub in _reg.GetSubKeyNames(uninstallRoot))
                {
                    string keyPath = uninstallRoot + "\\" + sub;
                    var displayName = _reg.GetValue(keyPath, "DisplayName") as string;
                    if (displayName == null || !string.Equals(displayName.Trim(), "Revit " + year, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var location = _reg.GetValue(keyPath, "InstallLocation") as string;
                    var inst = Build(year, displayName.Trim(), location);
                    if (inst != null) return inst;
                }
            }

            return null;
        }

        /// <summary>For the manual "locate Revit.exe" fallback when nothing was detected (SPEC §5.0).</summary>
        public RevitInstallation FromExePath(string exePath)
        {
            if (!_fs.FileExists(exePath)) return null;
            if (!string.Equals(Path.GetFileName(exePath), "Revit.exe", StringComparison.OrdinalIgnoreCase)) return null;

            string dir = Path.GetDirectoryName(exePath);
            int year = GuessYear(dir, _fs.GetFileVersion(exePath));
            if (year == 0) return null;
            return Build(year, "Revit " + year + " (manual)", dir);
        }

        private static int GuessYear(string installDir, string fileVersion)
        {
            var m = Regex.Match(installDir ?? string.Empty, @"20\d{2}");
            if (m.Success)
            {
                int y = int.Parse(m.Value);
                if (y >= MinYear && y <= MaxYear) return y;
            }
            // Revit.exe FileVersion major is the year minus 2000 (e.g. 24.x -> 2024).
            if (!string.IsNullOrEmpty(fileVersion))
            {
                var head = fileVersion.Split('.')[0];
                int major;
                if (int.TryParse(head, out major) && 2000 + major >= MinYear && 2000 + major <= MaxYear)
                    return 2000 + major;
            }
            return 0;
        }

        private RevitInstallation Build(int year, string productName, string installPath)
        {
            if (string.IsNullOrEmpty(installPath)) return null;

            string root = installPath.Trim().TrimEnd('\\');
            string exe = root + @"\Revit.exe";
            if (!_fs.FileExists(exe)) return null;

            string addins = root + @"\AddIns";
            string steel = addins + @"\SteelConnections";

            string disabled = null;
            var disabledDirs = _fs.GetDirectories(addins, "SteelConnections_disabled_*");
            if (disabledDirs != null && disabledDirs.Length > 0)
            {
                Array.Sort(disabledDirs, StringComparer.OrdinalIgnoreCase);
                disabled = disabledDirs[disabledDirs.Length - 1];
            }

            return new RevitInstallation
            {
                Year = year,
                ProductName = productName,
                InstallPath = root + "\\",
                ExePath = exe,
                ExeVersion = _fs.GetFileVersion(exe),
                SteelConnectionsPath = steel,
                SteelConnectionsExists = _fs.DirectoryExists(steel),
                DisabledSteelConnectionsPath = disabled
            };
        }
    }
}
