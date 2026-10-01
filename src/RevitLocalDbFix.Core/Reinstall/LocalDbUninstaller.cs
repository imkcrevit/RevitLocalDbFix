using System;
using System.IO;
using System.Text.RegularExpressions;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Process;

namespace RevitLocalDbFix.Core.Reinstall
{
    public sealed class UninstallEntry
    {
        public string ProductCode { get; set; }
        public string DisplayName { get; set; }
        public string DisplayVersion { get; set; }
        public string RegistryKeyPath { get; set; }
    }

    /// <summary>
    /// Step 2.5.1 (SPEC §5.2): finds and silently uninstalls ONLY the LocalDB version matching
    /// the profile's year — never other coexisting versions (SPEC §9 rule 2).
    /// [待实测 SPEC §13-4: msiexec /x exit codes and reboot requirement.]
    /// </summary>
    public sealed class LocalDbUninstaller
    {
        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        private readonly IRegistryReader _reg;
        private readonly ICommandRunner _runner;

        public LocalDbUninstaller(IRegistryReader reg, ICommandRunner runner)
        {
            _reg = reg ?? new RegistryReader64();
            _runner = runner;
        }

        public UninstallEntry Find(ProductProfile profile)
        {
            var rx = new Regex(profile.UninstallDisplayNameRegex, RegexOptions.IgnoreCase);
            foreach (var root in UninstallRoots)
            {
                foreach (var sub in _reg.GetSubKeyNames(root))
                {
                    string keyPath = root + "\\" + sub;
                    var displayName = _reg.GetValue(keyPath, "DisplayName") as string;
                    if (displayName == null || !rx.IsMatch(displayName)) continue;

                    return new UninstallEntry
                    {
                        ProductCode = sub,
                        DisplayName = displayName,
                        DisplayVersion = _reg.GetValue(keyPath, "DisplayVersion") as string,
                        RegistryKeyPath = "HKLM\\" + keyPath
                    };
                }
            }
            return null;
        }

        /// <summary>msiexec /x {ProductCode} /qn /norestart /l*v &lt;log&gt; (SPEC §5.2-2.5.1).</summary>
        public CommandResult Uninstall(UninstallEntry entry, string logPath)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            if (_runner == null) throw new InvalidOperationException("No command runner configured.");
            string msiexec = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
            string args = "/x " + entry.ProductCode + " /qn /norestart /l*v \"" + logPath + "\"";
            return _runner.Run(msiexec, args, TimeSpan.FromMinutes(10));
        }

        public static bool IsRebootRequired(int exitCode)
        {
            return exitCode == 3010;
        }
    }
}
