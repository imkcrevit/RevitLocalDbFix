using System;
using System.IO;

namespace RevitLocalDbFix.Core.Config
{
    public enum ProductKind
    {
        Revit = 0,
        AdvanceSteel
    }

    /// <summary>
    /// Product year -> required LocalDB engine / instance name / fixed paths (SPEC §3.1).
    /// The mapping rules are frozen by the SPEC; do not change them without updating the SPEC.
    /// </summary>
    public sealed class ProductProfile
    {
        public ProductKind Product { get; set; }
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
        /// <summary>Process names (without .exe) that must not run while this product's instance is changed.</summary>
        public string[] ProcessNames { get; set; }

        /// <summary>"Revit 2024" / "Advance Steel 2024".</summary>
        public string GetDisplayName()
        {
            return (Product == ProductKind.AdvanceSteel ? "Advance Steel " : "Revit ") + Year;
        }
    }

    public static class ProductProfiles
    {
        /// <summary>The automatic instance shared by 2018-2020 products; LocalDB creates it on first start.</summary>
        public const string AutomaticInstanceName = "MSSQLLocalDB";

        public static ProductProfile ForRevit(int year)
        {
            if (year < 2018)
                throw new ArgumentOutOfRangeException("year", "Revit versions before 2018 are not covered (SPEC §3.1).");

            string instance;
            if (year >= 2025)
            {
                // ART_LOCALDB_INVESTIGATE says "Starting with Revit 2024 --> SteelConnections202Xv15",
                // but FIELD-VERIFIED 2026-10-01 on a healthy machine (Revit 2024-2027 installed):
                // only 2024 uses the v15 suffix; 2025/2026/2027 are plain "SteelConnections<year>"
                // on the 15.0 engine. See docs/IMPLEMENTATION_NOTES.md.
                instance = "SteelConnections" + year;
            }
            else if (year == 2024)
            {
                instance = "SteelConnections2024v15";
            }
            else if (year >= 2021)
            {
                instance = "SteelConnections" + year;
            }
            else
            {
                instance = AutomaticInstanceName;
            }

            return Build(ProductKind.Revit, year, instance, new[] { "Revit" });
        }

        /// <summary>
        /// Advance Steel (ART_LOCALDB_INVESTIGATE): 2018-2020 use MSSQLLocalDB, 2021+ use a dedicated
        /// instance "AdvanceSteel&lt;year&gt;" (no v15 suffix, e.g. AdvanceSteel2024), 2024+ on SQL 2019.
        /// Advance Steel runs inside AutoCAD, so acad.exe is the process to guard.
        /// </summary>
        public static ProductProfile ForAdvanceSteel(int year)
        {
            if (year < 2018)
                throw new ArgumentOutOfRangeException("year", "Advance Steel versions before 2018 are not covered.");

            string instance = year >= 2021 ? "AdvanceSteel" + year : AutomaticInstanceName;
            return Build(ProductKind.AdvanceSteel, year, instance, new[] { "acad" });
        }

        public static ProductProfile For(ProductKind product, int year)
        {
            return product == ProductKind.AdvanceSteel ? ForAdvanceSteel(year) : ForRevit(year);
        }

        private static ProductProfile Build(ProductKind product, int year, string instance, string[] processNames)
        {
            string pf = Paths.ProgramFiles64;
            var profile = new ProductProfile
            {
                Product = product,
                Year = year,
                InstanceName = instance,
                ProcessNames = processNames
            };

            if (year >= 2024)
            {
                profile.LocalDbMajorVersion = "15.0";
                // 15.0.2104.1 per the article; 15.0.4382.1 (CU) observed on a healthy machine.
                profile.KnownFullVersions = new[] { "15.0.2104.1", "15.0.4382.1" };
                profile.SqlLocalDbExePath = Path.Combine(pf, "Microsoft SQL Server", "150", "Tools", "Binn", "SqlLocalDB.exe");
                profile.EngineBinnPath = Path.Combine(pf, "Microsoft SQL Server", "150", "LocalDB", "Binn");
                profile.ProgramFilesLocalDbFolder = Path.Combine(pf, "Microsoft SQL Server", "150", "LocalDB");
                profile.DownloadLinkId = "DL_LOCALDB_2019";
                profile.UninstallDisplayNameRegex = "Microsoft SQL Server 2019.*LocalDB";
                profile.LocalDbDisplayName = "SQL Server 2019 Express LocalDB";
            }
            else
            {
                profile.LocalDbMajorVersion = "12.0";
                profile.KnownFullVersions = new[] { "12.0.4100.1", "12.0.5000.0", "12.0.6024.0" };
                profile.SqlLocalDbExePath = Path.Combine(pf, "Microsoft SQL Server", "120", "Tools", "Binn", "SqlLocalDB.exe");
                profile.EngineBinnPath = Path.Combine(pf, "Microsoft SQL Server", "120", "LocalDB", "Binn");
                profile.ProgramFilesLocalDbFolder = Path.Combine(pf, "Microsoft SQL Server", "120", "LocalDB");
                profile.DownloadLinkId = "DL_LOCALDB_2014";
                profile.UninstallDisplayNameRegex = "Microsoft SQL Server 2014.*LocalDB";
                profile.LocalDbDisplayName = "SQL Server 2014 Express LocalDB (SP1/SP2/SP3)";
            }

            return profile;
        }
    }
}
