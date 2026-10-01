using System.IO;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Reinstall;

namespace RevitLocalDbFix.Core.SqlLocalDb
{
    /// <summary>Snapshot of how (and whether) the required LocalDB engine is installed.</summary>
    public sealed class LocalDbInstallState
    {
        public bool InstalledVersionKeyExists { get; set; }
        public bool SqlLocalDbExeExists { get; set; }
        public bool EngineSqlservrExists { get; set; }
        /// <summary>All "Installed Versions" sub keys, to surface coexisting engines (e.g. Visual Studio's).</summary>
        public string[] AllInstalledMajorVersions { get; set; }
        public UninstallEntry UninstallEntry { get; set; }
    }

    /// <summary>Read-only inspection of the LocalDB installation (SPEC §3.2, §5.2-2.5 verification).</summary>
    public sealed class LocalDbInstallationInspector
    {
        public const string InstalledVersionsKey = @"SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions";

        private readonly IRegistryReader _reg;
        private readonly IFileSystemProbe _fs;

        public LocalDbInstallationInspector() : this(new RegistryReader64(), new FileSystemProbe()) { }

        public LocalDbInstallationInspector(IRegistryReader reg, IFileSystemProbe fs)
        {
            _reg = reg;
            _fs = fs;
        }

        public LocalDbInstallState Inspect(ProductProfile profile)
        {
            return new LocalDbInstallState
            {
                InstalledVersionKeyExists = _reg.KeyExists(InstalledVersionsKey + "\\" + profile.LocalDbMajorVersion),
                SqlLocalDbExeExists = _fs.FileExists(profile.SqlLocalDbExePath),
                EngineSqlservrExists = _fs.FileExists(Path.Combine(profile.EngineBinnPath, "sqlservr.exe")),
                AllInstalledMajorVersions = _reg.GetSubKeyNames(InstalledVersionsKey),
                UninstallEntry = new LocalDbUninstaller(_reg, null).Find(profile)
            };
        }
    }
}
