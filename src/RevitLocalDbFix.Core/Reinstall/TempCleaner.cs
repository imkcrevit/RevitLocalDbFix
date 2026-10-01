using System.IO;

namespace RevitLocalDbFix.Core.Reinstall
{
    public sealed class TempCleanResult
    {
        public int DeletedFiles { get; set; }
        public int DeletedDirectories { get; set; }
        public int Skipped { get; set; }
        public long FreedBytes { get; set; }
    }

    /// <summary>
    /// Step 2.5.2 (SPEC §5.2): empties %temp%. Locked or access-denied entries are skipped and
    /// counted, matching the article's "skip on prompt" instruction. This is one of only two
    /// allowed delete operations (SPEC §9 rule 1).
    /// </summary>
    public sealed class TempCleaner
    {
        public TempCleanResult Clean()
        {
            var result = new TempCleanResult();
            string temp = Path.GetTempPath();

            foreach (var file in SafeGetFiles(temp))
            {
                try
                {
                    long size = new FileInfo(file).Length;
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    result.DeletedFiles++;
                    result.FreedBytes += size;
                }
                catch
                {
                    result.Skipped++;
                }
            }

            foreach (var dir in SafeGetDirectories(temp))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    result.DeletedDirectories++;
                }
                catch
                {
                    result.Skipped++;
                }
            }

            return result;
        }

        private static string[] SafeGetFiles(string path)
        {
            try { return Directory.GetFiles(path); }
            catch { return new string[0]; }
        }

        private static string[] SafeGetDirectories(string path)
        {
            try { return Directory.GetDirectories(path); }
            catch { return new string[0]; }
        }
    }
}
