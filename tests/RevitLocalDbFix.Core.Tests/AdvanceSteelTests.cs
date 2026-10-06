using System;
using System.Collections.Generic;
using RevitLocalDbFix.Core.AdvanceSteel;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;
using Xunit;

namespace RevitLocalDbFix.Core.Tests
{
    /// <summary>Advance Steel naming per ART_LOCALDB_INVESTIGATE ("AdvanceSteel202x", 2018-2020 MSSQLLocalDB).</summary>
    public class AdvanceSteelTests
    {
        [Theory]
        [InlineData(2018)]
        [InlineData(2020)]
        public void AdvanceSteel2018To2020_UseAutomaticInstance(int year)
        {
            var p = ProductProfiles.ForAdvanceSteel(year);
            Assert.Equal("MSSQLLocalDB", p.InstanceName);
            Assert.Equal("12.0", p.LocalDbMajorVersion);
        }

        [Theory]
        [InlineData(2021, "12.0")]
        [InlineData(2023, "12.0")]
        [InlineData(2024, "15.0")]
        [InlineData(2026, "15.0")]
        public void AdvanceSteel2021Plus_UseDedicatedInstance_NoV15Suffix(int year, string major)
        {
            var p = ProductProfiles.ForAdvanceSteel(year);
            Assert.Equal("AdvanceSteel" + year, p.InstanceName);
            Assert.Equal(major, p.LocalDbMajorVersion);
            Assert.Equal(ProductKind.AdvanceSteel, p.Product);
            Assert.Equal(new[] { "acad" }, p.ProcessNames);
            Assert.Equal("Advance Steel " + year, p.GetDisplayName());
        }

        [Fact]
        public void For_DispatchesByProduct()
        {
            Assert.Equal("SteelConnections2022", ProductProfiles.For(ProductKind.Revit, 2022).InstanceName);
            Assert.Equal("AdvanceSteel2022", ProductProfiles.For(ProductKind.AdvanceSteel, 2022).InstanceName);
            Assert.Equal(new[] { "Revit" }, ProductProfiles.ForRevit(2022).ProcessNames);
        }

        private sealed class FakeRegistry : IRegistryReader
        {
            public readonly Dictionary<string, string[]> SubKeys = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            public string[] GetSubKeyNames(string path) { string[] r; return SubKeys.TryGetValue(path, out r) ? r : new string[0]; }
            public object GetValue(string path, string name) { object r; return Values.TryGetValue(path + "::" + name, out r) ? r : null; }
            public bool KeyExists(string path) { return SubKeys.ContainsKey(path); }
        }

        [Fact]
        public void Locator_FindsYears_IgnoresRevitSteelConnections()
        {
            const string root = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var reg = new FakeRegistry();
            reg.SubKeys[root] = new[] { "{A}", "{B}", "{C}", "{D}" };
            reg.Values[root + @"\{A}::DisplayName"] = "Autodesk Advance Steel 2024";
            reg.Values[root + @"\{B}::DisplayName"] = "Advance Steel 2022 Language Pack - Chinese (Simplified)";
            reg.Values[root + @"\{C}::DisplayName"] = "Autodesk Steel Connections for Revit 2024";
            reg.Values[root + @"\{D}::DisplayName"] = "Autodesk Advance Steel 2024 - Update 2";

            var years = new AdvanceSteelInstallationLocator(reg).EnumerateYears();

            Assert.Equal(new[] { 2022, 2024 }, years);
        }
    }
}
