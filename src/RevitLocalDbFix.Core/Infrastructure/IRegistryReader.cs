namespace RevitLocalDbFix.Core.Infrastructure
{
    /// <summary>
    /// Read-only access to HKEY_LOCAL_MACHINE (64-bit view), abstracted so registry-based
    /// logic can be unit-tested with fake data (SPEC §11.1).
    /// Paths are relative to HKLM, e.g. @"SOFTWARE\Autodesk\Revit\2024".
    /// </summary>
    public interface IRegistryReader
    {
        /// <summary>Sub key names, or an empty array when the key does not exist.</summary>
        string[] GetSubKeyNames(string hklmKeyPath);

        /// <summary>A value, or null when the key/value does not exist.</summary>
        object GetValue(string hklmKeyPath, string valueName);

        bool KeyExists(string hklmKeyPath);
    }
}
