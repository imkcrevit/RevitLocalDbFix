using System;

namespace RevitLocalDbFix.Core.Process
{
    /// <summary>Raw result of one external command run (SPEC §7).</summary>
    public sealed class CommandResult
    {
        public CommandResult(string exePath, string arguments, int exitCode, string stdOut, string stdErr,
                             TimeSpan duration, DateTime startedAt, bool timedOut)
        {
            ExePath = exePath;
            Arguments = arguments ?? string.Empty;
            ExitCode = exitCode;
            StdOut = stdOut ?? string.Empty;
            StdErr = stdErr ?? string.Empty;
            Duration = duration;
            StartedAt = startedAt;
            TimedOut = timedOut;
        }

        public string ExePath { get; }
        public string Arguments { get; }
        public int ExitCode { get; }
        public string StdOut { get; }
        public string StdErr { get; }
        public TimeSpan Duration { get; }
        public DateTime StartedAt { get; }
        public bool TimedOut { get; }

        /// <summary>The full command line as shown to the user before execution (SPEC §5.2).</summary>
        public string CommandLine
        {
            get { return "\"" + ExePath + "\" " + Arguments; }
        }

        /// <summary>stdout followed by stderr, as displayed in the raw output pane.</summary>
        public string CombinedOutput
        {
            get
            {
                if (string.IsNullOrEmpty(StdErr)) return StdOut;
                if (string.IsNullOrEmpty(StdOut)) return StdErr;
                return StdOut.TrimEnd('\r', '\n') + Environment.NewLine + StdErr;
            }
        }

        public bool Succeeded
        {
            get { return !TimedOut && ExitCode == 0; }
        }
    }
}
