using System;
using System.IO;

namespace RevitLocalDbFix.Core.Config
{
    /// <summary>
    /// Well-known file system locations (SPEC §3.2).
    /// </summary>
    public static class Paths
    {
        public static string LocalAppData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        }

        /// <summary>64-bit Program Files even if the process ever runs 32-bit.</summary>
        public static string ProgramFiles64
        {
            get
            {
                return Environment.GetEnvironmentVariable("ProgramW6432")
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            }
        }

        // ---- Tool's own data (SPEC §3.2 last row) ----

        public static string ToolDataRoot { get { return Path.Combine(LocalAppData, "RevitLocalDbFix"); } }
        public static string StateFile { get { return Path.Combine(ToolDataRoot, "state.json"); } }
        public static string UiLanguageFile { get { return Path.Combine(ToolDataRoot, "ui-language.txt"); } }
        public static string BackupsDir { get { return Path.Combine(ToolDataRoot, "backups"); } }
        public static string DownloadsDir { get { return Path.Combine(ToolDataRoot, "downloads"); } }
        public static string LogsDir { get { return Path.Combine(ToolDataRoot, "logs"); } }
        public static string ReportsDir { get { return Path.Combine(ToolDataRoot, "reports"); } }

        // ---- LocalDB user data ----

        public static string LocalDbUserRoot
        {
            get { return Path.Combine(LocalAppData, "Microsoft", "Microsoft SQL Server Local DB"); }
        }

        public static string LocalDbInstancesDir
        {
            get { return Path.Combine(LocalDbUserRoot, "Instances"); }
        }

        // ---- Revit ----

        public static string JournalsDir(int year)
        {
            return Path.Combine(LocalAppData, "Autodesk", "Revit", "Autodesk Revit " + year, "Journals");
        }

        /// <summary>Steel connections database files, read-only informational check (SPEC §3.2).</summary>
        public static string SteelConnectionsProgramData(int year)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Autodesk", "Revit Steel Connections " + year);
        }

        public static void EnsureToolDirs()
        {
            Directory.CreateDirectory(ToolDataRoot);
            Directory.CreateDirectory(BackupsDir);
            Directory.CreateDirectory(DownloadsDir);
            Directory.CreateDirectory(LogsDir);
            Directory.CreateDirectory(ReportsDir);
        }
    }
}
