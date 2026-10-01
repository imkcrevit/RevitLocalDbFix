using System;
using System.IO;
using System.Text;
using RevitLocalDbFix.Core.Config;

namespace RevitLocalDbFix.Core.Infrastructure
{
    /// <summary>
    /// Minimal file logger writing to %LOCALAPPDATA%\RevitLocalDbFix\logs\ (SPEC §2).
    /// Logging must never throw into the caller.
    /// </summary>
    public static class FileLogger
    {
        private static readonly object Gate = new object();

        public static string CurrentLogFile
        {
            get { return Path.Combine(Paths.LogsDir, "RevitLocalDbFix_" + DateTime.Now.ToString("yyyyMMdd") + ".log"); }
        }

        public static void Log(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Paths.LogsDir);
                    File.AppendAllText(
                        CurrentLogFile,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // never fail the caller because of logging
            }
        }

        public static void Log(string message, Exception ex)
        {
            Log(message + " | " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
