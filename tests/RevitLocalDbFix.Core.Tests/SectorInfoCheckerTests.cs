using System;
using System.IO;
using RevitLocalDbFix.Core.Sector;
using Xunit;

namespace RevitLocalDbFix.Core.Tests
{
    public class SectorInfoCheckerTests
    {
        private static string Sample(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", name));
        }

        [Fact]
        public void ParseValue_HealthyDisk_Both4096()
        {
            string output = Sample("fsutil_sectorinfo_4096.txt");

            Assert.Equal(4096, SectorInfoChecker.ParseValue(output, SectorInfoChecker.KeyAtomicity));
            Assert.Equal(4096, SectorInfoChecker.ParseValue(output, SectorInfoChecker.KeyPerformance));
        }

        [Fact]
        public void ParseValue_ProblemDisk_Atomicity8192()
        {
            string output = Sample("fsutil_sectorinfo_8192.txt");

            Assert.Equal(8192, SectorInfoChecker.ParseValue(output, SectorInfoChecker.KeyAtomicity));
            Assert.Equal(4096, SectorInfoChecker.ParseValue(output, SectorInfoChecker.KeyPerformance));
        }

        [Fact]
        public void ParseValue_MissingLine_ReturnsNull()
        {
            Assert.Null(SectorInfoChecker.ParseValue("LogicalBytesPerSector : 512", SectorInfoChecker.KeyAtomicity));
            Assert.Null(SectorInfoChecker.ParseValue("", SectorInfoChecker.KeyAtomicity));
            Assert.Null(SectorInfoChecker.ParseValue(null, SectorInfoChecker.KeyAtomicity));
        }

        [Theory]
        [InlineData("PhysicalBytesPerSectorForAtomicity : 512", 512)]
        [InlineData("PhysicalBytesPerSectorForAtomicity:4096", 4096)]
        [InlineData("PhysicalBytesPerSectorForAtomicity :                    8192", 8192)]
        public void ParseValue_SpacingVariants(string line, long expected)
        {
            Assert.Equal(expected, SectorInfoChecker.ParseValue(line, SectorInfoChecker.KeyAtomicity));
        }
    }
}
