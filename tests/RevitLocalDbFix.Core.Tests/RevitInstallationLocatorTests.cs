using System;
using System.Collections.Generic;
using System.Linq;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Revit;
using Xunit;

namespace RevitLocalDbFix.Core.Tests
{
    public class RevitInstallationLocatorTests
    {
        private sealed class FakeRegistry : IRegistryReader
        {
            public readonly Dictionary<string, string[]> SubKeys =
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, object> Values =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public string[] GetSubKeyNames(string path)
            {
                string[] result;
                return SubKeys.TryGetValue(path, out result) ? result : new string[0];
            }

            public object GetValue(string path, string name)
            {
                object result;
                return Values.TryGetValue(path + "::" + name, out result) ? result : null;
            }

            public bool KeyExists(string path)
            {
                return SubKeys.ContainsKey(path);
            }
        }

        private sealed class FakeFileSystem : IFileSystemProbe
        {
            public readonly HashSet<string> Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public string Version = "24.2.30.84";

            public bool FileExists(string path) { return Files.Contains(path); }
            public bool DirectoryExists(string path) { return Dirs.Contains(path); }
            public string[] GetDirectories(string path, string pattern) { return new string[0]; }
            public string GetFileVersion(string exePath) { return Version; }
        }

        [Fact]
        public void Enumerate_FindsRevitFromAutodeskKey_NonDefaultPath()
        {
            var registry = new FakeRegistry();
            registry.SubKeys[@"SOFTWARE\Autodesk\Revit\2024"] = new[] { "REVIT-05:2052" };
            registry.Values[@"SOFTWARE\Autodesk\Revit\2024\REVIT-05:2052::InstallationLocation"] = @"D:\Apps\Revit 2024\";
            registry.Values[@"SOFTWARE\Autodesk\Revit\2024\REVIT-05:2052::ProductName"] = "Autodesk Revit 2024";

            var fs = new FakeFileSystem();
            fs.Files.Add(@"D:\Apps\Revit 2024\Revit.exe");
            fs.Dirs.Add(@"D:\Apps\Revit 2024\AddIns\SteelConnections");

            var found = new RevitInstallationLocator(registry, fs).Enumerate();

            Assert.Single(found);
            var revit = found.Single();
            Assert.Equal(2024, revit.Year);
            Assert.Equal(@"D:\Apps\Revit 2024\", revit.InstallPath);
            Assert.Equal(@"D:\Apps\Revit 2024\Revit.exe", revit.ExePath);
            Assert.Equal(@"D:\Apps\Revit 2024\AddIns\SteelConnections", revit.SteelConnectionsPath);
            Assert.True(revit.SteelConnectionsExists);
            Assert.Equal("Autodesk Revit 2024", revit.ProductName);
        }

        [Fact]
        public void Enumerate_FallsBackToUninstallEntry()
        {
            var registry = new FakeRegistry();
            registry.SubKeys[@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"] = new[] { "{GUID-2022}" };
            registry.Values[@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{GUID-2022}::DisplayName"] = "Revit 2022";
            registry.Values[@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{GUID-2022}::InstallLocation"] = @"C:\Program Files\Autodesk\Revit 2022";

            var fs = new FakeFileSystem();
            fs.Files.Add(@"C:\Program Files\Autodesk\Revit 2022\Revit.exe");

            var found = new RevitInstallationLocator(registry, fs).Enumerate();

            Assert.Single(found);
            Assert.Equal(2022, found[0].Year);
            Assert.False(found[0].SteelConnectionsExists);
        }

        [Fact]
        public void Enumerate_RegistryEntryWithoutExe_IsIgnored()
        {
            var registry = new FakeRegistry();
            registry.SubKeys[@"SOFTWARE\Autodesk\Revit\2023"] = new[] { "REVIT-05:1033" };
            registry.Values[@"SOFTWARE\Autodesk\Revit\2023\REVIT-05:1033::InstallationLocation"] = @"C:\Gone\Revit 2023\";

            var found = new RevitInstallationLocator(registry, new FakeFileSystem()).Enumerate();

            Assert.Empty(found);
        }
    }
}
