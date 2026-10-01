namespace RevitLocalDbFix.Core.SqlLocalDb
{
    /// <summary>Parsed output of "sqllocaldb i &lt;name&gt;" (SPEC §3.3, §5.2-2.2).</summary>
    public sealed class SqlLocalDbInstanceInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string SharedName { get; set; }
        public string Owner { get; set; }
        public bool AutoCreate { get; set; }
        public string State { get; set; }
        /// <summary>Kept as the raw string: the format depends on the OS locale.</summary>
        public string LastStartTime { get; set; }
        public string InstancePipeName { get; set; }

        /// <summary>"12.0" from "12.0.4100.1"; empty when the version is missing/unparsable.</summary>
        public string MajorVersion
        {
            get
            {
                if (string.IsNullOrEmpty(Version)) return string.Empty;
                var parts = Version.Split('.');
                return parts.Length >= 2 ? parts[0] + "." + parts[1] : parts[0];
            }
        }
    }
}
