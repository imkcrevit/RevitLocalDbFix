using System;
using System.Collections.Generic;
using System.Linq;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Process;
using RevitLocalDbFix.Core.Provisioning;
using Xunit;

namespace RevitLocalDbFix.Core.Tests
{
    public class InstanceProvisionerTests
    {
        /// <summary>Fake sqllocaldb: answers by argument prefix, records every call.</summary>
        private sealed class FakeRunner : ICommandRunner
        {
            public readonly Dictionary<string, string> Answers = new Dictionary<string, string>();
            public readonly List<string> Calls = new List<string>();

            public CommandResult Run(string exePath, string arguments, TimeSpan? timeout = null)
            {
                Calls.Add(arguments);
                string output;
                Answers.TryGetValue(arguments, out output);
                return new CommandResult(exePath, arguments, 0, output ?? string.Empty, string.Empty,
                                         TimeSpan.Zero, DateTime.Now, false);
            }
        }

        private sealed class FakeFs : IFileSystemProbe
        {
            public bool EngineInstalled = true;
            public bool FileExists(string path) { return EngineInstalled; }
            public bool DirectoryExists(string path) { return false; }
            public string[] GetDirectories(string path, string pattern) { return new string[0]; }
            public string GetFileVersion(string exePath) { return null; }
        }

        private static string Info(string name, string version)
        {
            return "Name:               " + name + "\r\nVersion:            " + version +
                   "\r\nShared name:\r\nOwner:              DOMAIN\\user\r\nAuto-create:        No\r\nState:              Stopped\r\n";
        }

        // Year 2099 keeps the instance name away from anything real on the test machine.
        private static readonly ProductProfile Revit2099 = ProductProfiles.ForRevit(2099);

        [Fact]
        public void Inspect_InstanceNotListed_IsMissing_WithPinnedCreatePreview()
        {
            var runner = new FakeRunner();
            runner.Answers["i"] = "SteelConnections2024v15\r\n";
            var plan = new InstanceProvisioner(runner, new FakeFs(), _ => false).Inspect(Revit2099);

            Assert.Equal(InstanceStatus.Missing, plan.Status);
            Assert.True(plan.CanCreate);
            Assert.EndsWith("create \"SteelConnections2099\" 15.0", plan.CreateCommandPreview);
        }

        [Fact]
        public void Inspect_ListedAndRightMajor_IsHealthy()
        {
            var runner = new FakeRunner();
            runner.Answers["i"] = "SteelConnections2099\r\n";
            runner.Answers["i \"SteelConnections2099\""] = Info("SteelConnections2099", "15.0.4382.1");
            var plan = new InstanceProvisioner(runner, new FakeFs(), _ => false).Inspect(Revit2099);

            Assert.Equal(InstanceStatus.Healthy, plan.Status);
            Assert.Equal("15.0.4382.1", plan.CurrentVersion);
            Assert.False(plan.CanCreate);
        }

        [Fact]
        public void Inspect_ListedOnOtherMajor_IsWrongVersion_NotCreatable()
        {
            var runner = new FakeRunner();
            runner.Answers["i"] = "SteelConnections2099\r\n";
            runner.Answers["i \"SteelConnections2099\""] = Info("SteelConnections2099", "16.0.1000.6");
            var plan = new InstanceProvisioner(runner, new FakeFs(), _ => false).Inspect(Revit2099);

            Assert.Equal(InstanceStatus.WrongVersion, plan.Status);
            Assert.False(plan.CanCreate);
        }

        [Fact]
        public void Inspect_EngineNotInstalled_IsEngineMissing_RunsNothing()
        {
            var runner = new FakeRunner();
            var plan = new InstanceProvisioner(runner, new FakeFs { EngineInstalled = false }, _ => false).Inspect(Revit2099);

            Assert.Equal(InstanceStatus.EngineMissing, plan.Status);
            Assert.Empty(runner.Calls);
        }

        [Fact]
        public void Inspect_2018To2020_IsAutomaticInstance_NeverCreatable()
        {
            var runner = new FakeRunner();
            var plan = new InstanceProvisioner(runner, new FakeFs(), _ => false).Inspect(ProductProfiles.ForRevit(2019));

            Assert.Equal(InstanceStatus.AutomaticInstance, plan.Status);
            Assert.False(plan.CanCreate);
            Assert.Null(plan.CreateCommandPreview);
        }

        [Fact]
        public void Inspect_ArticleStyleV15NameFor2025_IsReportedAsAlternate()
        {
            // The article's "Starting with Revit 2024 --> SteelConnections202Xv15" applied to 2025.
            var runner = new FakeRunner();
            runner.Answers["i"] = "SteelConnections2024v15\r\nSteelConnections2025v15\r\n";
            var plan = new InstanceProvisioner(runner, new FakeFs(), _ => false).Inspect(ProductProfiles.ForRevit(2025));

            Assert.Equal(InstanceStatus.Missing, plan.Status);
            Assert.Equal("SteelConnections2025v15", plan.AlternateNameFound);
        }

        [Fact]
        public void AlternateNames_CoverBothDirectionsOfTheV15Suffix()
        {
            Assert.Equal(new[] { "SteelConnections2024" }, InstanceProvisioner.AlternateNames(ProductProfiles.ForRevit(2024)).ToArray());
            Assert.Equal(new[] { "SteelConnections2026v15" }, InstanceProvisioner.AlternateNames(ProductProfiles.ForRevit(2026)).ToArray());
            Assert.Equal(new[] { "AdvanceSteel2024v15" }, InstanceProvisioner.AlternateNames(ProductProfiles.ForAdvanceSteel(2024)).ToArray());
            Assert.Empty(InstanceProvisioner.AlternateNames(ProductProfiles.ForRevit(2022)));
            Assert.Empty(InstanceProvisioner.AlternateNames(ProductProfiles.ForRevit(2020)));
        }

        [Fact]
        public void InspectAll_SharesOneListCallPerEngine()
        {
            var runner = new FakeRunner();
            runner.Answers["i"] = string.Empty;
            new InstanceProvisioner(runner, new FakeFs(), _ => false)
                .InspectAll(new[] { ProductProfiles.ForRevit(2097), ProductProfiles.ForRevit(2098), ProductProfiles.ForRevit(2099) });

            Assert.Equal(1, runner.Calls.Count(c => c == "i"));
        }

        [Fact]
        public void Create_Missing_RunsPinnedCreate_AndVerifies()
        {
            var runner = new FakeRunner();
            runner.Answers["i"] = string.Empty;
            runner.Answers["create \"SteelConnections2099\" 15.0"] =
                "LocalDB instance \"SteelConnections2099\" created with version 15.0.4382.1.";
            runner.Answers["i \"SteelConnections2099\""] = Info("SteelConnections2099", "15.0.4382.1");

            var result = new InstanceProvisioner(runner, new FakeFs(), _ => false).Create(Revit2099, "unused");

            Assert.True(result.Success, result.Error);
            Assert.Equal("15.0.4382.1", result.CreatedVersion);
            Assert.Contains("create \"SteelConnections2099\" 15.0", runner.Calls);
            Assert.Null(result.BackupZipPath);
        }

        [Fact]
        public void Create_ProductRunning_RefusesWithoutRunningAnything()
        {
            var runner = new FakeRunner();
            var result = new InstanceProvisioner(runner, new FakeFs(), name => name == "Revit").Create(Revit2099, "unused");

            Assert.False(result.Success);
            Assert.Contains("Revit.exe", result.Error);
            Assert.Empty(runner.Calls);
        }

        [Fact]
        public void Create_ExistingInstance_IsNeverReplaced()
        {
            var runner = new FakeRunner();
            runner.Answers["i"] = "SteelConnections2099\r\n";
            runner.Answers["i \"SteelConnections2099\""] = Info("SteelConnections2099", "15.0.4382.1");

            var result = new InstanceProvisioner(runner, new FakeFs(), _ => false).Create(Revit2099, "unused");

            Assert.False(result.Success);
            Assert.DoesNotContain(runner.Calls, c => c.StartsWith("create") || c.StartsWith("delete"));
        }

        [Fact]
        public void Create_LandsOnWrongMajor_IsReportedAsFailure()
        {
            var runner = new FakeRunner();
            runner.Answers["i"] = string.Empty;
            runner.Answers["create \"SteelConnections2099\" 15.0"] =
                "LocalDB instance \"SteelConnections2099\" created with version 16.0.1000.6.";
            runner.Answers["i \"SteelConnections2099\""] = Info("SteelConnections2099", "16.0.1000.6");

            var result = new InstanceProvisioner(runner, new FakeFs(), _ => false).Create(Revit2099, "unused");

            Assert.False(result.Success);
            Assert.Contains("15.0", result.Error);
        }
    }
}
