using System;
using System.IO;

namespace RevitLocalDbFix.Core.Config
{
    /// <summary>
    /// Revit year -> required LocalDB engine / instance name / fixed paths (SPEC §3.1).
    /// The mapping rules are frozen by the SPEC; do not change them without updating the SPEC.
    /// </summary>
    public sealed class ProductProfile
    {
        public int Year { get; set; }
        public string InstanceName { get; set; }
        /// <summary>"12.0" (SQL 2014) or "15.0" (SQL 2019).</summary>
        public string LocalDbMajorVersion { get; set; }
        public string[] KnownFullVersions { get; set; }
        public string SqlLocalDbExePath { get; set; }
        public string EngineBinnPath { get; set; }
        public string ProgramFilesLocalDbFolder { get; set; }
        public string DownloadLinkId { get; set; }
        public string UninstallDisplayNameRegex { get; set; }
        public string LocalDbDisplayName { get; set; }
    }

    public static class ProductProfiles
    {
        public static ProductProfile ForRevit(int year)
        {
            if (year < 2018)
                throw new ArgumentOutOfRangeException("year", "Revit versions before 2018 are not covered (SPEC §3.1).");

            string pf = Paths.ProgramFiles64;

            if (year >= 2024)
            {
                // SPEC §3.1 says "SteelConnections<year>v15" for 2024+, but FIELD-VERIFIED
                // 2026-10-01 on a healthy machine (Revit 2024-2027 installed): only 2024 uses
                // the v15 suffix; 2025/2026/2027 are plain "SteelConnections<year>" on the
                // 15.0 engine. See docs/IMPLEMENTATION_NOTES.md.
                return new ProductProfile
                {
                    Year = year,
                    InstanceName = "SteelConnections" + year + (year == 2024 ? "v15" : string.Empty),
                    LocalDbMajorVersion = "15.0",
                    // 15.0.2104.1 per SPEC; 15.0.4382.1 (CU) observed on a healthy machine.
                    KnownFullVersions = new[] { "15.0.2104.1", "15.0.4382.1" },
                    SqlLocalDbExePath = Path.Combine(pf, "Microsoft SQL Server", "150", "Tools", "Binn", "SqlLocalDB.exe"),
                    EngineBinnPath = Path.Combine(pf, "Microsoft SQL Server", "150", "LocalDB", "Binn"),
                    ProgramFilesLocalDbFolder = Path.Combine(pf, "Microsoft SQL Server", "150", "LocalDB"),
                    DownloadLinkId = "DL_LOCALDB_2019",
                    UninstallDisplayNameRegex = "Microsoft SQL Server 2019.*LocalDB",
                    LocalDbDisplayName = "SQL Server 2019 Express LocalDB"
                };
            }

            return new ProductProfile
            {
                Year = year,
                // 2018-2020 share the default MSSQLLocalDB instance; 2021-2023 use per-year instances.
                InstanceName = year >= 2021 ? "SteelConnections" + year : "MSSQLLocalDB",
                LocalDbMajorVersion = "12.0",
                KnownFullVersions = new[] { "12.0.4100.1", "12.0.5000.0", "12.0.6024.0" },
                SqlLocalDbExePath = Path.Combine(pf, "Microsoft SQL Server", "120", "Tools", "Binn", "SqlLocalDB.exe"),
                EngineBinnPath = Path.Combine(pf, "Microsoft SQL Server", "120", "LocalDB", "Binn"),
                ProgramFilesLocalDbFolder = Path.Combine(pf, "Microsoft SQL Server", "120", "LocalDB"),
                DownloadLinkId = "DL_LOCALDB_2014",
                UninstallDisplayNameRegex = "Microsoft SQL Server 2014.*LocalDB",
                LocalDbDisplayName = "SQL Server 2014 Express LocalDB (SP1/SP2/SP3)"
            };
        }

        /// <summary>Reserved for phase 2 (SPEC §3.1): Advance Steel 2021+ uses instance "AdvanceSteel&lt;year&gt;".</summary>
        public static ProductProfile ForAdvanceSteel(int year)
        {
            throw new NotSupportedException("Advance Steel profiles are reserved for a later phase (SPEC §3.1).");
        }
    }
}
