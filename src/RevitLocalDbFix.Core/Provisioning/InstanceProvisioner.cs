using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Process;
using RevitLocalDbFix.Core.SqlLocalDb;

namespace RevitLocalDbFix.Core.Provisioning
{
    public enum InstanceStatus
    {
        /// <summary>Exists, readable, on the required major version.</summary>
        Healthy = 0,
        /// <summary>Not registered — can be added with a pinned version.</summary>
        Missing,
        /// <summary>Exists on another major version — needs 2.4 recreate, not an add.</summary>
        WrongVersion,
        /// <summary>"sqllocaldb i &lt;name&gt;" output not in the documented format — 2.5 territory.</summary>
        Unreadable,
        /// <summary>The required engine's SqlLocalDB.exe is not installed — 2.5 reinstall first.</summary>
        EngineMissing,
        /// <summary>MSSQLLocalDB (2018-2020): created by LocalDB itself, never by "create".</summary>
        AutomaticInstance
    }

    /// <summary>What the tool found for one product's expected instance.</summary>
    public sealed class InstancePlan
    {
        public ProductProfile Profile { get; set; }
        public InstanceStatus Status { get; set; }
        public string CurrentVersion { get; set; }
        /// <summary>
        /// An instance under a wrong but plausible name, e.g. "SteelConnections2025v15" created by following
        /// the article's "2024+ -> v15" rule literally while Revit 2025 actually uses "SteelConnections2025".
        /// Reported only; never deleted by the tool.
        /// </summary>
        public string AlternateNameFound { get; set; }
        /// <summary>Exact command the add step would run (pinned version).</summary>
        public string CreateCommandPreview { get; set; }
        public string RawOutput { get; set; }

        public bool CanCreate
        {
            get { return Status == InstanceStatus.Missing; }
        }
    }

    public sealed class ProvisionResult
    {
        public bool Success { get; set; }
        public string InstanceName { get; set; }
        public string CreatedVersion { get; set; }
        public string BackupZipPath { get; set; }
        public List<CommandResult> Commands { get; } = new List<CommandResult>();
        public string Error { get; set; }
    }

    /// <summary>
    /// Adds missing LocalDB instances for installed Revit / Advance Steel versions, based on
    /// ART_LOCALDB_INVESTIGATE ("Delete and Recreate the SQL instance", create part) with the
    /// tool's hard rules: version always pinned (SPEC §9-3), only the product's own instance,
    /// never while the product runs (§9-4), leftover instance folder zipped first (§9-1),
    /// existing instances are never replaced here.
    /// LocalDB instances are per Windows user: they are created for the user running the tool.
    /// </summary>
    public sealed class InstanceProvisioner
    {
        private readonly ICommandRunner _runner;
        private readonly IFileSystemProbe _fs;
        private readonly Func<string, bool> _isProcessRunning;

        public InstanceProvisioner()
            : this(new CommandRunner(), new FileSystemProbe(), DefaultIsProcessRunning) { }

        public InstanceProvisioner(ICommandRunner runner, IFileSystemProbe fs, Func<string, bool> isProcessRunning)
        {
            _runner = runner ?? throw new ArgumentNullException("runner");
            _fs = fs ?? throw new ArgumentNullException("fs");
            _isProcessRunning = isProcessRunning ?? throw new ArgumentNullException("isProcessRunning");
        }

        public static bool DefaultIsProcessRunning(string processName)
        {
            return System.Diagnostics.Process.GetProcessesByName(processName).Length > 0;
        }

        /// <summary>Names a user may have created by mistake for this profile (v15 suffix added or missing).</summary>
        public static IEnumerable<string> AlternateNames(ProductProfile profile)
        {
            string name = profile.InstanceName;
            if (string.Equals(name, ProductProfiles.AutomaticInstanceName, StringComparison.OrdinalIgnoreCase))
                yield break;
            if (name.EndsWith("v15", StringComparison.OrdinalIgnoreCase))
                yield return name.Substring(0, name.Length - 3);
            else if (profile.LocalDbMajorVersion == "15.0")
                yield return name + "v15";
        }

        public IReadOnlyList<InstancePlan> InspectAll(IEnumerable<ProductProfile> profiles)
        {
            var listCache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            return profiles.Select(p => Inspect(p, listCache)).ToList();
        }

        public InstancePlan Inspect(ProductProfile profile)
        {
            return Inspect(profile, new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
        }

        private InstancePlan Inspect(ProductProfile profile, Dictionary<string, IReadOnlyList<string>> listCache)
        {
            var client = new SqlLocalDbClient(profile.SqlLocalDbExePath, _runner);
            var plan = new InstancePlan
            {
                Profile = profile,
                CreateCommandPreview = "\"" + profile.SqlLocalDbExePath + "\" create \"" + profile.InstanceName + "\" " +
                                       profile.LocalDbMajorVersion
            };

            if (string.Equals(profile.InstanceName, ProductProfiles.AutomaticInstanceName, StringComparison.OrdinalIgnoreCase))
            {
                plan.Status = InstanceStatus.AutomaticInstance;
                plan.CreateCommandPreview = null;
                return plan;
            }

            if (!_fs.FileExists(profile.SqlLocalDbExePath))
            {
                plan.Status = InstanceStatus.EngineMissing;
                return plan;
            }

            IReadOnlyList<string> names;
            if (!listCache.TryGetValue(profile.SqlLocalDbExePath, out names))
            {
                var list = client.ListInstances();
                names = SqlLocalDbOutputParser.ParseInstanceList(list.CombinedOutput);
                listCache[profile.SqlLocalDbExePath] = names;
            }

            plan.AlternateNameFound = AlternateNames(profile)
                .FirstOrDefault(alt => names.Any(n => string.Equals(n, alt, StringComparison.OrdinalIgnoreCase)));

            if (!names.Any(n => string.Equals(n, profile.InstanceName, StringComparison.OrdinalIgnoreCase)))
            {
                plan.Status = InstanceStatus.Missing;
                return plan;
            }

            var info = client.Info(profile.InstanceName);
            plan.RawOutput = info.CombinedOutput;
            SqlLocalDbInstanceInfo parsed;
            if (!SqlLocalDbOutputParser.TryParseInstanceInfo(info.CombinedOutput, out parsed))
            {
                plan.Status = InstanceStatus.Unreadable;
                return plan;
            }

            plan.CurrentVersion = parsed.Version;
            plan.Status = parsed.MajorVersion == profile.LocalDbMajorVersion
                ? InstanceStatus.Healthy
                : InstanceStatus.WrongVersion;
            return plan;
        }

        /// <summary>Creates the instance only when it is genuinely missing. Never replaces an existing one.</summary>
        public ProvisionResult Create(ProductProfile profile, string backupDir)
        {
            var result = new ProvisionResult { InstanceName = profile.InstanceName };

            foreach (var proc in profile.ProcessNames ?? new string[0])
            {
                if (_isProcessRunning(proc))
                {
                    result.Error = proc + ".exe is running. Close it before adding the instance (SPEC §9-4).";
                    return result;
                }
            }

            var plan = Inspect(profile);
            if (plan.Status != InstanceStatus.Missing)
            {
                result.Error = "Instance \"" + profile.InstanceName + "\" is not missing (status: " + plan.Status +
                               "); the add step only creates missing instances.";
                return result;
            }

            try
            {
                string leftover = LocalDbInstancePaths.InstanceDir(profile.InstanceName);
                if (Directory.Exists(leftover) && Directory.EnumerateFileSystemEntries(leftover).Any())
                {
                    Directory.CreateDirectory(backupDir);
                    result.BackupZipPath = Path.Combine(
                        backupDir,
                        "Instance_" + profile.InstanceName + "_leftover_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");
                    ZipFile.CreateFromDirectory(leftover, result.BackupZipPath);
                }
            }
            catch (Exception ex)
            {
                result.Error = "Could not back up the leftover instance folder, nothing was created: " + ex.Message;
                return result;
            }

            var client = new SqlLocalDbClient(profile.SqlLocalDbExePath, _runner);

            var create = client.Create(profile.InstanceName, profile.LocalDbMajorVersion);
            result.Commands.Add(create);
            FileLogger.Log("Add instance: " + create.CommandLine + " -> exit " + create.ExitCode + " | " + create.CombinedOutput.Trim());

            string version;
            if (!SqlLocalDbOutputParser.TryParseCreatedVersion(create.CombinedOutput, profile.InstanceName, out version))
            {
                result.Error = "Create did not report success: " + create.CombinedOutput.Trim();
                return result;
            }
            result.CreatedVersion = version;

            var info = client.Info(profile.InstanceName);
            result.Commands.Add(info);
            SqlLocalDbInstanceInfo parsed;
            if (!SqlLocalDbOutputParser.TryParseInstanceInfo(info.CombinedOutput, out parsed))
            {
                result.Error = "Instance was created but its details could not be read back.";
                return result;
            }
            if (parsed.MajorVersion != profile.LocalDbMajorVersion)
            {
                result.Error = "Instance was created on " + parsed.Version + " but " + profile.LocalDbMajorVersion +
                               " is required.";
                return result;
            }

            result.Success = true;
            return result;
        }
    }
}
