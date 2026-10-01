using System;
using System.IO;

namespace RevitLocalDbFix.Core.Reinstall
{
    public sealed class ArchiveResult
    {
        public bool Success { get; set; }
        /// <summary>True when the folder did not exist (nothing to do, matches article's note).</summary>
        public bool NotFound { get; set; }
        public string OldPath { get; set; }
        public string NewPath { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Steps 2.5.3 / 2.5.4 (SPEC §5.2): renames LocalDB folders to *_OLD — never deletes
    /// (SPEC §9 rule 1). When *_OLD already exists, appends a timestamp.
    /// </summary>
    public sealed class LocalDbFolderArchiver
    {
        public ArchiveResult RenameToOld(string path)
        {
            var result = new ArchiveResult { OldPath = path };

            if (!Directory.Exists(path))
            {
                result.Success = true;
                result.NotFound = true;
                return result;
            }

            string target = path + "_OLD";
            if (Directory.Exists(target))
                target = path + "_OLD_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            try
            {
                Directory.Move(path, target);
                result.NewPath = target;
                result.Success = true;
            }
            catch (Exception ex)
            {
                // Typically the folder is locked by sqlservr.exe or a service:
                // the caller should offer a reboot and resume (SPEC §13-6).
                result.Success = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }

            return result;
        }
    }
}
