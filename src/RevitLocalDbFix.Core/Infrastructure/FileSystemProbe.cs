using System.Diagnostics;
using System.IO;

namespace RevitLocalDbFix.Core.Infrastructure
{
    public sealed class FileSystemProbe : IFileSystemProbe
    {
        public bool FileExists(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        public string[] GetDirectories(string path, string searchPattern)
        {
            try
            {
                if (!Directory.Exists(path)) return new string[0];
                return Directory.GetDirectories(path, searchPattern);
            }
            catch
            {
                return new string[0];
            }
        }

        public string GetFileVersion(string exePath)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(exePath).FileVersion;
            }
            catch
            {
                return null;
            }
        }
    }
}
