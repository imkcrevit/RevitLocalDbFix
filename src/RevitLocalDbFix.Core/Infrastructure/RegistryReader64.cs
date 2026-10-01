using System;
using Microsoft.Win32;

namespace RevitLocalDbFix.Core.Infrastructure
{
    public sealed class RegistryReader64 : IRegistryReader
    {
        public string[] GetSubKeyNames(string hklmKeyPath)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(hklmKeyPath))
                {
                    return key == null ? new string[0] : key.GetSubKeyNames();
                }
            }
            catch
            {
                return new string[0];
            }
        }

        public object GetValue(string hklmKeyPath, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(hklmKeyPath))
                {
                    return key == null ? null : key.GetValue(valueName);
                }
            }
            catch
            {
                return null;
            }
        }

        public bool KeyExists(string hklmKeyPath)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(hklmKeyPath))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
