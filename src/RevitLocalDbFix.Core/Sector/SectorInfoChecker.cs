using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Process;

namespace RevitLocalDbFix.Core.Sector
{
    public sealed class SectorInfoResult
    {
        public string Drive { get; set; }
        public bool FsutilSucceeded { get; set; }
        public string RawOutput { get; set; }
        public long? AtomicityBytes { get; set; }
        public long? PerformanceBytes { get; set; }
        public bool RegistryPatchApplied { get; set; }
    }

    /// <summary>
    /// Step 2.0 disk sector check (SPEC §5.2-2.0, article ART_REVIT_HANG):
    /// runs "fsutil fsinfo sectorinfo &lt;drive&gt;" (admin required) and reads the stornvme patch state.
    /// </summary>
    public sealed class SectorInfoChecker
    {
        public const string RegKeyPath = @"SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device";
        public const string RegValueName = "ForcedPhysicalSectorSizeInBytes";
        /// <summary>[待实测 SPEC §13-2] must match the content of the official SetSectorSize.reg.</summary>
        public const string RegExpectedEntry = "* 4095";

        public const string KeyAtomicity = "PhysicalBytesPerSectorForAtomicity";
        public const string KeyPerformance = "PhysicalBytesPerSectorForPerformance";

        private readonly ICommandRunner _runner;
        private readonly IRegistryReader _reg;

        public SectorInfoChecker() : this(new CommandRunner(), new RegistryReader64()) { }

        public SectorInfoChecker(ICommandRunner runner, IRegistryReader reg)
        {
            _runner = runner;
            _reg = reg;
        }

        public SectorInfoResult Check(string driveLetter)
        {
            string drive = NormalizeDrive(driveLetter);
            string fsutil = Path.Combine(Environment.SystemDirectory, "fsutil.exe");
            var run = _runner.Run(fsutil, "fsinfo sectorinfo " + drive);

            return new SectorInfoResult
            {
                Drive = drive,
                FsutilSucceeded = run.Succeeded,
                RawOutput = run.CombinedOutput,
                AtomicityBytes = ParseValue(run.CombinedOutput, KeyAtomicity),
                PerformanceBytes = ParseValue(run.CombinedOutput, KeyPerformance),
                RegistryPatchApplied = IsPatchApplied(_reg)
            };
        }

        /// <summary>Extracts a numeric fsutil value like "PhysicalBytesPerSectorForAtomicity : 4096".</summary>
        public static long? ParseValue(string fsutilOutput, string key)
        {
            if (string.IsNullOrEmpty(fsutilOutput)) return null;
            var m = Regex.Match(fsutilOutput, Regex.Escape(key) + @"\s*:\s*(\d+)");
            if (!m.Success) return null;
            long value;
            return long.TryParse(m.Groups[1].Value, out value) ? value : (long?)null;
        }

        public static bool IsPatchApplied(IRegistryReader reg)
        {
            var value = reg.GetValue(RegKeyPath, RegValueName);
            var multi = value as string[];
            if (multi != null) return multi.Any(s => (s ?? string.Empty).Trim() == RegExpectedEntry);
            var single = value as string;
            return single != null && single.Trim() == RegExpectedEntry;
        }

        private static string NormalizeDrive(string driveLetter)
        {
            string d = (driveLetter ?? "C").Trim().TrimEnd('\\', '/');
            if (!d.EndsWith(":")) d += ":";
            return d;
        }
    }
}
