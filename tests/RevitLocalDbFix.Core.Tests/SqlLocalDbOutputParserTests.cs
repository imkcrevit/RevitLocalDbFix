using System;
using System.IO;
using RevitLocalDbFix.Core.SqlLocalDb;
using Xunit;

namespace RevitLocalDbFix.Core.Tests
{
    public class SqlLocalDbOutputParserTests
    {
        private static string Sample(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", name));
        }

        // ---- instance list (SPEC §3.3 sample) ----

        [Fact]
        public void ParseInstanceList_SpecSample_ReturnsAllSixNames()
        {
            var list = SqlLocalDbOutputParser.ParseInstanceList(Sample("sqllocaldb_i_list.txt"));

            Assert.Equal(6, list.Count);
            Assert.Contains("MSSQLLocalDB", list);
            Assert.Contains("SteelConnections2022", list);
            Assert.Contains("SteelConnections2024v15", list);
        }

        [Fact]
        public void ParseInstanceList_WithBomAndCrLf_StillParses()
        {
            string output = "﻿MSSQLLocalDB\r\nSteelConnections2022\r\n";
            var list = SqlLocalDbOutputParser.ParseInstanceList(output);

            Assert.Equal(2, list.Count);
            Assert.Equal("MSSQLLocalDB", list[0]);
        }

        [Fact]
        public void ParseInstanceList_Empty_ReturnsEmpty()
        {
            Assert.Empty(SqlLocalDbOutputParser.ParseInstanceList(""));
            Assert.Empty(SqlLocalDbOutputParser.ParseInstanceList(null));
            Assert.Empty(SqlLocalDbOutputParser.ParseInstanceList("   \r\n  \r\n"));
        }

        // ---- instance info (SPEC §3.3 sample) ----

        [Fact]
        public void TryParseInstanceInfo_SpecSample_AllFields()
        {
            SqlLocalDbInstanceInfo info;
            bool ok = SqlLocalDbOutputParser.TryParseInstanceInfo(Sample("sqllocaldb_i_info.txt"), out info);

            Assert.True(ok);
            Assert.Equal("SteelConnections2022", info.Name);
            Assert.Equal("12.0.4100.1", info.Version);
            Assert.Equal("12.0", info.MajorVersion);
            Assert.Equal("DOMAIN\\user", info.Owner);
            Assert.True(info.AutoCreate);
            Assert.Equal("Stopped", info.State);
            Assert.Equal("23/10/2018 20:39:51", info.LastStartTime);
            Assert.Equal(string.Empty, info.SharedName);
            Assert.Equal(string.Empty, info.InstancePipeName);
        }

        [Fact]
        public void TryParseInstanceInfo_ChineseOutput_Parses()
        {
            // Real output captured 2026-10-01 from the 12.0 SqlLocalDB.exe on a zh-CN system
            // (the 12.0 exe speaks the OS language; the 15.0 exe outputs English).
            SqlLocalDbInstanceInfo info;
            bool ok = SqlLocalDbOutputParser.TryParseInstanceInfo(Sample("sqllocaldb_i_info_zh.txt"), out info);

            Assert.True(ok);
            Assert.Equal("SteelConnections2021", info.Name);
            Assert.Equal("12.0.6024.0", info.Version);
            Assert.Equal("12.0", info.MajorVersion);
            Assert.Equal("DOMAIN\\user", info.Owner);
            Assert.False(info.AutoCreate); // 否
            Assert.Equal("已停止", info.State);
            Assert.False(SqlLocalDbOutputParser.IsRunningState(info.State));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Windows API call FAILED")]
        [InlineData("The specified instance does not exist.")]
        [InlineData("找不到指定的 LocalDB 实例。")]
        public void TryParseInstanceInfo_ErrorOutputs_ReturnFalse(string output)
        {
            SqlLocalDbInstanceInfo info;
            Assert.False(SqlLocalDbOutputParser.TryParseInstanceInfo(output, out info));
            Assert.Null(info);
        }

        // ---- start / stop / delete / create messages (SPEC §3.3) ----

        [Fact]
        public void IsStartedMessage_Matches()
        {
            Assert.True(SqlLocalDbOutputParser.IsStartedMessage(
                "LocalDB instance \"SteelConnections2022\" started.", "SteelConnections2022"));
            Assert.False(SqlLocalDbOutputParser.IsStartedMessage(
                "LocalDB instance \"Other\" started.", "SteelConnections2022"));
        }

        [Fact]
        public void IsStoppedMessage_Matches()
        {
            Assert.True(SqlLocalDbOutputParser.IsStoppedMessage(
                "LocalDB instance \"SteelConnections2022\" stopped.", "SteelConnections2022"));
        }

        [Fact]
        public void IsDeletedMessage_Matches()
        {
            Assert.True(SqlLocalDbOutputParser.IsDeletedMessage(
                "LocalDB instance \"SteelConnections2022\" deleted.", "SteelConnections2022"));
        }

        [Fact]
        public void TryParseCreatedVersion_SpecSample()
        {
            string version;
            bool ok = SqlLocalDbOutputParser.TryParseCreatedVersion(
                "LocalDB instance \"SteelConnections2022\" created with version 12.0.4100.1.",
                "SteelConnections2022", out version);

            Assert.True(ok);
            Assert.Equal("12.0.4100.1", version);
        }

        [Fact]
        public void TryParseCreatedVersion_WrongInstance_ReturnsFalse()
        {
            string version;
            Assert.False(SqlLocalDbOutputParser.TryParseCreatedVersion(
                "LocalDB instance \"Other\" created with version 12.0.4100.1.",
                "SteelConnections2022", out version));
        }

        // ---- localized (zh-CN) messages from the 12.0 exe ----

        [Fact]
        public void IsStartedMessage_Chinese_Matches()
        {
            Assert.True(SqlLocalDbOutputParser.IsStartedMessage(
                "LocalDB 实例“SteelConnections2021”已启动。", "SteelConnections2021"));
            Assert.False(SqlLocalDbOutputParser.IsStartedMessage(
                "LocalDB 实例“Other”已启动。", "SteelConnections2021"));
        }

        [Fact]
        public void IsStoppedMessage_Chinese_Matches()
        {
            Assert.True(SqlLocalDbOutputParser.IsStoppedMessage(
                "LocalDB 实例“SteelConnections2021”已停止。", "SteelConnections2021"));
        }

        [Fact]
        public void IsDeletedMessage_Chinese_Matches()
        {
            Assert.True(SqlLocalDbOutputParser.IsDeletedMessage(
                "LocalDB 实例“SteelConnections2021”已删除。", "SteelConnections2021"));
        }

        [Fact]
        public void TryParseCreatedVersion_Chinese_ExtractsVersion()
        {
            string version;
            bool ok = SqlLocalDbOutputParser.TryParseCreatedVersion(
                "已使用版本 12.0.6024.0 创建 LocalDB 实例“SteelConnections2021”。",
                "SteelConnections2021", out version);

            Assert.True(ok);
            Assert.Equal("12.0.6024.0", version);
        }
    }
}
