namespace RevitLocalDbFix.Core.Infrastructure
{
    /// <summary>File system checks abstracted for unit testing (SPEC §11.1).</summary>
    public interface IFileSystemProbe
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        /// <summary>Matching sub directories, or an empty array when the parent does not exist.</summary>
        string[] GetDirectories(string path, string searchPattern);
        /// <summary>FileVersion string of an executable, or null.</summary>
        string GetFileVersion(string exePath);
    }
}
