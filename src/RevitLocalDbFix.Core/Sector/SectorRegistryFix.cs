using Microsoft.Win32;
using RevitLocalDbFix.Core.Infrastructure;

namespace RevitLocalDbFix.Core.Sector
{
    /// <summary>
    /// Writes the official sector size workaround (SPEC §3.2, source DL_SECTOR_REG).
    /// [待实测 SPEC §13-2]: key, value name and data must be verified against the downloaded
    /// SetSectorSize.reg before this ships; the reg file content wins on any difference.
    /// </summary>
    public sealed class SectorRegistryFix
    {
        public static readonly string[] ValueData = { SectorInfoChecker.RegExpectedEntry };

        public bool IsApplied()
        {
            return SectorInfoChecker.IsPatchApplied(new RegistryReader64());
        }

        /// <summary>Requires elevation. A reboot is required afterwards (SPEC §5.2-2.0).</summary>
        public void Apply()
        {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = baseKey.CreateSubKey(SectorInfoChecker.RegKeyPath))
            {
                key.SetValue(SectorInfoChecker.RegValueName, ValueData, RegistryValueKind.MultiString);
            }
        }

        /// <summary>Human-readable description shown in the confirmation dialog (SPEC §6.2).</summary>
        public string DescribeChange()
        {
            return "HKLM\\" + SectorInfoChecker.RegKeyPath + "\r\n  "
                 + SectorInfoChecker.RegValueName + " (REG_MULTI_SZ) = " + SectorInfoChecker.RegExpectedEntry;
        }
    }
}
