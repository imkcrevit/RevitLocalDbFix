using System;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace RevitLocalDbFix.Core.Elevation
{
    /// <summary>UAC elevation and reboot-resume plumbing (SPEC §4).</summary>
    public static class ElevationHelper
    {
        public const string RunOnceValueName = "RevitLocalDbFix";
        private const string RunOnceKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

        public static bool IsElevated()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static string CurrentExePath
        {
            get
            {
                using (var p = System.Diagnostics.Process.GetCurrentProcess())
                {
                    return p.MainModule.FileName;
                }
            }
        }

        /// <summary>
        /// Starts a new elevated copy of this exe. Throws Win32Exception when the user cancels UAC.
        /// The caller is responsible for exiting the current process afterwards.
        /// </summary>
        public static void RelaunchElevated(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = CurrentExePath,
                Arguments = args ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(psi);
        }

        /// <summary>Registers "&lt;exe&gt; --resume" in HKLM RunOnce for continuation after a reboot (SPEC §4).</summary>
        public static void RegisterRunOnceResume()
        {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = baseKey.CreateSubKey(RunOnceKeyPath))
            {
                key.SetValue(RunOnceValueName, "\"" + CurrentExePath + "\" --resume");
            }
        }

        public static void UnregisterRunOnceResume()
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(RunOnceKeyPath, writable: true))
                {
                    if (key != null) key.DeleteValue(RunOnceValueName, throwOnMissingValue: false);
                }
            }
            catch
            {
                // best effort
            }
        }
    }
}
