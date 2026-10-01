using System;
using System.Text.RegularExpressions;
using RevitLocalDbFix.Core.Config;
using Xunit;

namespace RevitLocalDbFix.Core.Tests
{
    /// <summary>Locks the SPEC §3.1 mapping: any change here must be a deliberate SPEC change.</summary>
    public class ProductProfilesTests
    {
        [Theory]
        [InlineData(2018)]
        [InlineData(2019)]
        [InlineData(2020)]
        public void Revit2018To2020_UseDefaultInstance_Sql2014(int year)
        {
            var profile = ProductProfiles.ForRevit(year);

            Assert.Equal("MSSQLLocalDB", profile.InstanceName);
            Assert.Equal("12.0", profile.LocalDbMajorVersion);
            Assert.EndsWith(@"\120\Tools\Binn\SqlLocalDB.exe", profile.SqlLocalDbExePath);
            Assert.EndsWith(@"\120\LocalDB\Binn", profile.EngineBinnPath);
            Assert.Equal("DL_LOCALDB_2014", profile.DownloadLinkId);
        }

        [Theory]
        [InlineData(2021)]
        [InlineData(2022)]
        [InlineData(2023)]
        public void Revit2021To2023_UsePerYearInstance_Sql2014(int year)
        {
            var profile = ProductProfiles.ForRevit(year);

            Assert.Equal("SteelConnections" + year, profile.InstanceName);
            Assert.Equal("12.0", profile.LocalDbMajorVersion);
            Assert.Contains("12.0.6024.0", profile.KnownFullVersions); // SP3
            Assert.Contains("12.0.4100.1", profile.KnownFullVersions); // SP1
            Assert.Contains("12.0.5000.0", profile.KnownFullVersions); // SP2
        }

        [Fact]
        public void Revit2024_UsesV15Suffix_Sql2019()
        {
            // FIELD-VERIFIED 2026-10-01: only 2024 carries the v15 suffix.
            var profile = ProductProfiles.ForRevit(2024);

            Assert.Equal("SteelConnections2024v15", profile.InstanceName);
            Assert.Equal("15.0", profile.LocalDbMajorVersion);
            Assert.EndsWith(@"\150\Tools\Binn\SqlLocalDB.exe", profile.SqlLocalDbExePath);
            Assert.Equal("DL_LOCALDB_2019", profile.DownloadLinkId);
        }

        [Theory]
        [InlineData(2025)]
        [InlineData(2026)]
        [InlineData(2027)]
        public void Revit2025Plus_PlainInstanceName_Sql2019(int year)
        {
            // FIELD-VERIFIED 2026-10-01: SteelConnections2025/2026/2027 exist without the
            // v15 suffix, engine 15.0.4382.1, on a healthy machine.
            var profile = ProductProfiles.ForRevit(year);

            Assert.Equal("SteelConnections" + year, profile.InstanceName);
            Assert.Equal("15.0", profile.LocalDbMajorVersion);
            Assert.Contains("15.0.2104.1", profile.KnownFullVersions);
            Assert.Contains("15.0.4382.1", profile.KnownFullVersions);
            Assert.Equal("DL_LOCALDB_2019", profile.DownloadLinkId);
        }

        [Fact]
        public void Before2018_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProductProfiles.ForRevit(2017));
        }

        [Fact]
        public void UninstallRegex_MatchesRealDisplayNames_OnlyOwnYear()
        {
            var p2014 = ProductProfiles.ForRevit(2022);
            var p2019 = ProductProfiles.ForRevit(2024);

            Assert.Matches(new Regex(p2014.UninstallDisplayNameRegex, RegexOptions.IgnoreCase),
                           "Microsoft SQL Server 2014 Express LocalDB");
            Assert.DoesNotMatch(new Regex(p2014.UninstallDisplayNameRegex, RegexOptions.IgnoreCase),
                                "Microsoft SQL Server 2019 Express LocalDB");

            Assert.Matches(new Regex(p2019.UninstallDisplayNameRegex, RegexOptions.IgnoreCase),
                           "Microsoft SQL Server 2019 Express LocalDB");
            Assert.DoesNotMatch(new Regex(p2019.UninstallDisplayNameRegex, RegexOptions.IgnoreCase),
                                "Microsoft SQL Server 2014 Express LocalDB");
        }
    }
}
