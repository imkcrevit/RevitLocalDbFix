using System.Collections.Generic;
using System.IO;
using System.Text;
using RevitLocalDbFix.Core.Config;

namespace RevitLocalDbFix.Core.SqlLocalDb
{
    /// <summary>User-profile instance folder helpers (SPEC §3.2).</summary>
    public static class LocalDbInstancePaths
    {
        public static string InstanceDir(string instanceName)
        {
            return Path.Combine(Paths.LocalDbInstancesDir, instanceName);
        }

        public static string ErrorLog(string instanceName)
        {
            return Path.Combine(InstanceDir(instanceName), "error.log");
        }

        /// <summary>Last N lines of the instance error.log; empty when the file is missing/unreadable.</summary>
        public static string[] TailErrorLog(string instanceName, int lines)
        {
            try
            {
                string path = ErrorLog(instanceName);
                if (!File.Exists(path)) return new string[0];

                var all = new List<string>();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs, Encoding.Default, detectEncodingFromByteOrderMarks: true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null) all.Add(line);
                }

                if (all.Count <= lines) return all.ToArray();
                return all.GetRange(all.Count - lines, lines).ToArray();
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
