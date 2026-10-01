using System;
using System.IO;
using RevitLocalDbFix.Core.Process;

namespace RevitLocalDbFix.Core.SqlLocalDb
{
    /// <summary>
    /// Thin wrapper around the version-specific SqlLocalDB.exe. Always called by its full path
    /// (120 or 150 Tools\Binn), never via PATH (SPEC §9 rule 6).
    /// </summary>
    public sealed class SqlLocalDbClient
    {
        private readonly string _exePath;
        private readonly ICommandRunner _runner;

        public SqlLocalDbClient(string exePath, ICommandRunner runner)
        {
            if (string.IsNullOrEmpty(exePath)) throw new ArgumentException("exePath is required", "exePath");
            _exePath = exePath;
            _runner = runner ?? throw new ArgumentNullException("runner");
        }

        public string ExePath
        {
            get { return _exePath; }
        }

        public bool ExeExists
        {
            get { return File.Exists(_exePath); }
        }

        public CommandResult Versions()
        {
            return Run("versions");
        }

        public CommandResult ListInstances()
        {
            return Run("i");
        }

        public CommandResult Info(string instance)
        {
            return Run("i \"" + instance + "\"");
        }

        public CommandResult Start(string instance)
        {
            return Run("start \"" + instance + "\"");
        }

        public CommandResult Stop(string instance, bool kill = false)
        {
            return Run("stop \"" + instance + "\"" + (kill ? " -k" : string.Empty));
        }

        public CommandResult Delete(string instance)
        {
            return Run("delete \"" + instance + "\"");
        }

        /// <summary>
        /// SPEC §9 rule 3: the version MUST always be pinned so the instance is never created
        /// with a newer coexisting engine (irreversible database upgrade, SPEC §3.1).
        /// Note: the real CLI syntax is positional — create "name" 12.0 — there is no -v flag;
        /// SPEC §3.3 writes "-v" but means "pin the version" (docs/IMPLEMENTATION_NOTES.md).
        /// </summary>
        public CommandResult Create(string instance, string majorVersion)
        {
            if (string.IsNullOrWhiteSpace(majorVersion))
                throw new ArgumentException("The LocalDB version must be pinned explicitly (SPEC §9 rule 3).", "majorVersion");
            return Run("create \"" + instance + "\" " + majorVersion.Trim());
        }

        private CommandResult Run(string arguments)
        {
            return _runner.Run(_exePath, arguments);
        }
    }
}
