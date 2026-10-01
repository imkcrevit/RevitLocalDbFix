using System.IO;
using System.Runtime.Serialization.Json;
using RevitLocalDbFix.Core.Config;

namespace RevitLocalDbFix.Core.Workflow
{
    /// <summary>
    /// Persists the session to %LOCALAPPDATA%\RevitLocalDbFix\state.json (SPEC §10).
    /// Uses DataContractJsonSerializer so the client machine needs no extra runtime dependency.
    /// </summary>
    public sealed class SessionStateStore
    {
        private readonly string _path;

        public SessionStateStore() : this(Paths.StateFile) { }

        public SessionStateStore(string path)
        {
            _path = path;
        }

        private static DataContractJsonSerializer CreateSerializer()
        {
            var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
            return new DataContractJsonSerializer(typeof(WizardSession), settings);
        }

        /// <summary>Returns null when there is no stored session or it cannot be read.</summary>
        public WizardSession Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                using (var fs = File.OpenRead(_path))
                {
                    return (WizardSession)CreateSerializer().ReadObject(fs);
                }
            }
            catch
            {
                return null;
            }
        }

        public void Save(WizardSession session)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(_path))
            {
                CreateSerializer().WriteObject(fs, session);
            }
        }

        public void Clear()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch { /* best effort */ }
        }
    }
}
