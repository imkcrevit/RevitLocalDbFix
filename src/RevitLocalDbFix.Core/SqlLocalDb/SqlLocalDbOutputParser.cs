using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RevitLocalDbFix.Core.SqlLocalDb
{
    /// <summary>
    /// Text parsers for sqllocaldb output (SPEC §3.3). Fully unit-tested with fixed samples,
    /// tolerant to BOM/CRLF (SPEC §11.1).
    /// FIELD-VERIFIED 2026-10-01: the SQL 2014 (12.0) SqlLocalDB.exe speaks the OS language
    /// (zh-CN labels like 名称:/版本:), while the 2019 (15.0) exe outputs English on the same
    /// machine — so every parser here accepts both English and zh-CN forms.
    /// </summary>
    public static class SqlLocalDbOutputParser
    {
        /// <summary>Localized label -> canonical English label (observed zh-CN output of 12.0).</summary>
        private static readonly Dictionary<string, string> KeyAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Name", "Name" },                       { "名称", "Name" },
                { "Version", "Version" },                 { "版本", "Version" },
                { "Shared name", "Shared name" },         { "共享名称", "Shared name" },
                { "Owner", "Owner" },                     { "所有者", "Owner" },
                { "Auto-create", "Auto-create" },         { "自动创建", "Auto-create" },
                { "State", "State" },                     { "状态", "State" },
                { "Last start time", "Last start time" }, { "上次启动时间", "Last start time" },
                { "Instance pipe name", "Instance pipe name" }, { "实例管道名称", "Instance pipe name" }
            };

        private static readonly char[] ColonChars = { ':', '：' }; // ASCII and full-width colon

        private static IEnumerable<string> Lines(string output)
        {
            if (string.IsNullOrEmpty(output)) yield break;
            foreach (var raw in output.Split('\n'))
            {
                yield return raw.Trim('\r', ' ', '\t', '﻿');
            }
        }

        /// <summary>Instance names from "sqllocaldb i": one name per non-empty line.</summary>
        public static IReadOnlyList<string> ParseInstanceList(string output)
        {
            var result = new List<string>();
            foreach (var line in Lines(output))
            {
                if (line.Length > 0) result.Add(line);
            }
            return result;
        }

        /// <summary>
        /// Parses "sqllocaldb i &lt;name&gt;" details. Returns false when the output does not
        /// have the documented shape (which SPEC §5.2-2.2 treats as "LocalDB not installed correctly").
        /// </summary>
        public static bool TryParseInstanceInfo(string output, out SqlLocalDbInstanceInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(output)) return false;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in Lines(output))
            {
                int idx = line.IndexOfAny(ColonChars);
                if (idx <= 0) continue;
                string rawKey = line.Substring(0, idx).Trim();
                string canonicalKey;
                if (!KeyAliases.TryGetValue(rawKey, out canonicalKey)) continue;
                string value = line.Substring(idx + 1).Trim();
                if (!map.ContainsKey(canonicalKey)) map.Add(canonicalKey, value);
            }

            string name, version;
            if (!map.TryGetValue("Name", out name) || name.Length == 0) return false;
            if (!map.TryGetValue("Version", out version) || !Regex.IsMatch(version, @"^\d+\.")) return false;

            string sharedName, owner, autoCreate, state, lastStart, pipe;
            map.TryGetValue("Shared name", out sharedName);
            map.TryGetValue("Owner", out owner);
            map.TryGetValue("Auto-create", out autoCreate);
            map.TryGetValue("State", out state);
            map.TryGetValue("Last start time", out lastStart);
            map.TryGetValue("Instance pipe name", out pipe);

            info = new SqlLocalDbInstanceInfo
            {
                Name = name,
                Version = version,
                SharedName = sharedName ?? string.Empty,
                Owner = owner ?? string.Empty,
                AutoCreate = IsYes(autoCreate),
                State = state ?? string.Empty,
                LastStartTime = lastStart ?? string.Empty,
                InstancePipeName = pipe ?? string.Empty
            };
            return true;
        }

        /// <summary>"Yes" (en) or "是" (zh-CN).</summary>
        private static bool IsYes(string value)
        {
            return string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "是", StringComparison.Ordinal);
        }

        /// <summary>"Running" (en) or "正在运行" (zh-CN).</summary>
        public static bool IsRunningState(string state)
        {
            return string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "正在运行", StringComparison.Ordinal);
        }

        public static bool IsStartedMessage(string output, string instance)
        {
            return ContainsMessage(output, instance, "started", "已启动");
        }

        public static bool IsStoppedMessage(string output, string instance)
        {
            return ContainsMessage(output, instance, "stopped", "已停止");
        }

        public static bool IsDeletedMessage(string output, string instance)
        {
            return ContainsMessage(output, instance, "deleted", "已删除");
        }

        /// <summary>
        /// en: LocalDB instance "X" created with version 12.0.4100.1.
        /// zh (FIELD-VERIFIED 2026-10-01, real create on a zh-CN system):
        ///     已使用版本 12.0.6024.0 创建 LocalDB 实例“X”。
        /// </summary>
        public static bool TryParseCreatedVersion(string output, string instance, out string version)
        {
            version = null;
            if (string.IsNullOrEmpty(output)) return false;

            var m = Regex.Match(
                output,
                "LocalDB instance \"" + Regex.Escape(instance) + "\" created with version ([0-9][0-9.]*)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                version = m.Groups[1].Value.TrimEnd('.');
                return true;
            }

            if (output.IndexOf(instance, StringComparison.OrdinalIgnoreCase) >= 0 &&
                output.IndexOf("创建", StringComparison.Ordinal) >= 0)
            {
                var zh = Regex.Match(output, @"(\d+\.\d+\.\d+\.\d+)");
                if (zh.Success)
                {
                    version = zh.Groups[1].Value;
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMessage(string output, string instance, string englishVerb, string chineseToken)
        {
            if (string.IsNullOrEmpty(output)) return false;

            string needle = "LocalDB instance \"" + instance + "\" " + englishVerb;
            if (output.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // Localized output (12.0 exe): instance name may be wrapped in full-width quotes,
            // so match name and the localized verb independently.
            return output.IndexOf(instance, StringComparison.OrdinalIgnoreCase) >= 0 &&
                   output.IndexOf(chineseToken, StringComparison.Ordinal) >= 0;
        }
    }
}
