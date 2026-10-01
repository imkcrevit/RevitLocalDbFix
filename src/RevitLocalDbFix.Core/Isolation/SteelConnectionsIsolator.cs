using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using RevitLocalDbFix.Core.Elevation;
using RevitLocalDbFix.Core.Revit;

namespace RevitLocalDbFix.Core.Isolation
{
    public sealed class BackupResult
    {
        public bool Success { get; set; }
        public string ZipPath { get; set; }
        public int SourceFileCount { get; set; }
        public int ZipEntryCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public string Sha256 { get; set; }
        public string Error { get; set; }
    }

    public sealed class DisableResult
    {
        public bool Success { get; set; }
        public string DisabledPath { get; set; }
        public string Error { get; set; }
    }

    public sealed class RestoreResult
    {
        public bool Success { get; set; }
        /// <summary>"AlreadyPresent" | "Renamed" | "Unzipped".</summary>
        public string Method { get; set; }
        public string RestoredPath { get; set; }
        public int FileCount { get; set; }
        public string Error { get; set; }
    }

    public sealed class PreflightResult
    {
        public List<string> Issues { get; } = new List<string>();
        public bool Ok { get { return Issues.Count == 0; } }
        public bool RevitRunning { get; set; }
        public bool Elevated { get; set; }
        public bool SteelFolderExists { get; set; }
        public string DisabledFolderPath { get; set; }
        public long RequiredBytes { get; set; }
        public long FreeBytes { get; set; }
    }

    /// <summary>
    /// Step 1 isolation: zip backup + rename (never delete) of AddIns\SteelConnections (SPEC §5.1, §9 rule 1).
    /// </summary>
    public sealed class SteelConnectionsIsolator
    {
        public const string DisabledPrefix = "SteelConnections_disabled_";

        public PreflightResult Preflight(RevitInstallation revit, string backupDir)
        {
            var result = new PreflightResult();

            result.RevitRunning = System.Diagnostics.Process.GetProcessesByName("Revit").Length > 0;
            if (result.RevitRunning)
                result.Issues.Add("Revit.exe is running. Close Revit before continuing (SPEC §9 rule 4).");

            result.Elevated = ElevationHelper.IsElevated();
            if (!result.Elevated)
                result.Issues.Add("Administrator rights are required to modify Program Files (SPEC §4).");

            result.SteelFolderExists = Directory.Exists(revit.SteelConnectionsPath);
            if (!result.SteelFolderExists)
            {
                result.DisabledFolderPath = FindDisabledFolder(revit);
                result.Issues.Add(result.DisabledFolderPath != null
                    ? "SteelConnections is already disabled: " + result.DisabledFolderPath
                    : "SteelConnections folder not found: the module is not installed for this version (skip step 1, SPEC §5.1).");
                return result;
            }

            try
            {
                result.RequiredBytes = (long)(DirectorySize(revit.SteelConnectionsPath) * 1.2);
                var root = Path.GetPathRoot(Path.GetFullPath(backupDir));
                result.FreeBytes = new DriveInfo(root).AvailableFreeSpace;
                if (result.FreeBytes < result.RequiredBytes)
                    result.Issues.Add("Not enough free space on the backup drive: need " + result.RequiredBytes +
                                      " bytes, have " + result.FreeBytes + " bytes.");
            }
            catch (Exception ex)
            {
                result.Issues.Add("Could not verify free disk space: " + ex.Message);
            }

            return result;
        }

        /// <summary>Zip backup with entry-count verification and SHA256 (SPEC §5.1).</summary>
        public BackupResult Backup(RevitInstallation revit, string backupDir)
        {
            var result = new BackupResult();
            try
            {
                string source = revit.SteelConnectionsPath;
                Directory.CreateDirectory(backupDir);
                result.ZipPath = Path.Combine(
                    backupDir,
                    "SteelConnections_" + revit.Year + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");

                var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                result.SourceFileCount = files.Length;
                result.TotalSizeBytes = files.Sum(f => new FileInfo(f).Length);

                ZipFile.CreateFromDirectory(source, result.ZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

                using (var archive = ZipFile.OpenRead(result.ZipPath))
                {
                    // directory entries have an empty Name; count real files only
                    result.ZipEntryCount = archive.Entries.Count(e => !string.IsNullOrEmpty(e.Name));
                }

                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(result.ZipPath))
                {
                    result.Sha256 = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                }

                result.Success = result.ZipEntryCount == result.SourceFileCount;
                if (!result.Success)
                    result.Error = "Zip verification failed: " + result.ZipEntryCount + " entries vs " +
                                   result.SourceFileCount + " source files.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            return result;
        }

        /// <summary>Renames the folder to SteelConnections_disabled_&lt;timestamp&gt; — never deletes (SPEC §9 rule 1).</summary>
        public DisableResult Disable(RevitInstallation revit)
        {
            var result = new DisableResult();
            try
            {
                string source = revit.SteelConnectionsPath;
                string parent = Path.GetDirectoryName(source);
                string target = Path.Combine(parent, DisabledPrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.Move(source, target);
                result.DisabledPath = target;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            return result;
        }

        /// <summary>Rename back first; fall back to unzipping the backup (SPEC §5.1 还原).</summary>
        public RestoreResult Restore(RevitInstallation revit, string zipFallback)
        {
            var result = new RestoreResult();
            try
            {
                string target = revit.SteelConnectionsPath;

                if (Directory.Exists(target))
                {
                    result.Method = "AlreadyPresent";
                    result.RestoredPath = target;
                    result.FileCount = Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length;
                    result.Success = true;
                    return result;
                }

                string disabled = FindDisabledFolder(revit);
                if (disabled != null)
                {
                    Directory.Move(disabled, target);
                    result.Method = "Renamed";
                }
                else if (!string.IsNullOrEmpty(zipFallback) && File.Exists(zipFallback))
                {
                    ZipFile.ExtractToDirectory(zipFallback, target);
                    result.Method = "Unzipped";
                }
                else
                {
                    result.Success = false;
                    result.Error = "Nothing to restore: no disabled folder and no backup zip found.";
                    return result;
                }

                result.RestoredPath = target;
                result.FileCount = Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length;
                result.Success = result.FileCount > 0;
                if (!result.Success) result.Error = "Restored folder is empty.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            return result;
        }

        private static string FindDisabledFolder(RevitInstallation revit)
        {
            if (!string.IsNullOrEmpty(revit.DisabledSteelConnectionsPath) &&
                Directory.Exists(revit.DisabledSteelConnectionsPath))
                return revit.DisabledSteelConnectionsPath;

            try
            {
                string parent = Path.GetDirectoryName(revit.SteelConnectionsPath);
                if (parent == null || !Directory.Exists(parent)) return null;
                var candidates = Directory.GetDirectories(parent, DisabledPrefix + "*");
                if (candidates.Length == 0) return null;
                Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
                return candidates[candidates.Length - 1];
            }
            catch
            {
                return null;
            }
        }

        private static long DirectorySize(string path)
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                            .Sum(f => new FileInfo(f).Length);
        }
    }
}
