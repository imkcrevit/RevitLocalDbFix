using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RevitLocalDbFix.Core.Config;

namespace RevitLocalDbFix.Core.Revit
{
    public sealed class JournalHit
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string Line { get; set; }
        public DateTime FileTime { get; set; }
    }

    /// <summary>
    /// Read-only scan of recent Revit journals for the official warning text (SPEC §1.1, §5.0).
    /// </summary>
    public sealed class JournalScanner
    {
        public const string WarningToken = "SQLLocalDB instance is malfunctioning";

        public IReadOnlyList<JournalHit> FindMalfunctionWarnings(int year, int maxFiles = 20, DateTime? newerThan = null)
        {
            var hits = new List<JournalHit>();
            string dir = Paths.JournalsDir(year);
            if (!Directory.Exists(dir)) return hits;

            IEnumerable<FileInfo> files;
            try
            {
                files = new DirectoryInfo(dir)
                    .GetFiles("journal*.txt")
                    .OrderByDescending(f => f.LastWriteTime)
                    .Where(f => newerThan == null || f.LastWriteTime > newerThan.Value)
                    .Take(maxFiles);
            }
            catch
            {
                return hits;
            }

            foreach (var file in files)
            {
                try
                {
                    // Journals can be held open by a running Revit; read shared.
                    using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs, Encoding.Default, detectEncodingFromByteOrderMarks: true))
                    {
                        string line;
                        int lineNumber = 0;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lineNumber++;
                            if (line.IndexOf(WarningToken, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                hits.Add(new JournalHit
                                {
                                    FilePath = file.FullName,
                                    LineNumber = lineNumber,
                                    Line = line.Trim(),
                                    FileTime = file.LastWriteTime
                                });
                                break; // one hit per file is enough for the overview
                            }
                        }
                    }
                }
                catch
                {
                    // unreadable journal: skip silently, this scan is informational only
                }
            }

            return hits;
        }
    }
}
