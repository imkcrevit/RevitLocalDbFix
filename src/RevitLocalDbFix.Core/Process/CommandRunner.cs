using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RevitLocalDbFix.Core.Process
{
    /// <summary>
    /// Runs an external console command, captures stdout/stderr without deadlocks,
    /// and decodes the output with the OEM code page so Chinese-locale consoles parse correctly.
    /// </summary>
    public sealed class CommandRunner : ICommandRunner
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

        [DllImport("kernel32.dll")]
        private static extern int GetOEMCP();

        private static Encoding ConsoleEncoding()
        {
            try { return Encoding.GetEncoding(GetOEMCP()); }
            catch { return Encoding.Default; }
        }

        public CommandResult Run(string exePath, string arguments, TimeSpan? timeout = null)
        {
            var startedAt = DateTime.Now;
            var sw = Stopwatch.StartNew();
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var enc = ConsoleEncoding();
            int exitCode = -1;
            bool timedOut = false;

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = enc,
                StandardErrorEncoding = enc
            };

            using (var p = new System.Diagnostics.Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (stderr) stderr.AppendLine(e.Data); };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                int ms = (int)(timeout ?? DefaultTimeout).TotalMilliseconds;
                if (p.WaitForExit(ms))
                {
                    p.WaitForExit(); // flush remaining async output
                    exitCode = p.ExitCode;
                }
                else
                {
                    timedOut = true;
                    try { p.Kill(); } catch { /* already gone */ }
                }
            }

            sw.Stop();
            string so, se;
            lock (stdout) so = stdout.ToString();
            lock (stderr) se = stderr.ToString();
            return new CommandResult(exePath, arguments, exitCode, so, se, sw.Elapsed, startedAt, timedOut);
        }
    }
}
