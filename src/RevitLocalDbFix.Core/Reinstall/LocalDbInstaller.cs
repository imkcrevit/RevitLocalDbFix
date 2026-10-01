using System;
using System.IO;
using RevitLocalDbFix.Core.Process;

namespace RevitLocalDbFix.Core.Reinstall
{
    /// <summary>
    /// Step 2.5.6 (SPEC §5.2): silent msi install. Exit code 3010 means a reboot is required,
    /// which the caller handles via RunOnce resume (SPEC §4).
    /// </summary>
    public sealed class LocalDbInstaller
    {
        private readonly ICommandRunner _runner;

        public LocalDbInstaller(ICommandRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException("runner");
        }

        public CommandResult Install(string msiPath, string logPath)
        {
            string msiexec = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
            string args = "/i \"" + msiPath + "\" IACCEPTSQLLOCALDBLICENSETERMS=YES /qn /norestart /l*v \"" + logPath + "\"";
            return _runner.Run(msiexec, args, TimeSpan.FromMinutes(15));
        }

        public static bool IsRebootRequired(int exitCode)
        {
            return exitCode == 3010;
        }
    }
}
