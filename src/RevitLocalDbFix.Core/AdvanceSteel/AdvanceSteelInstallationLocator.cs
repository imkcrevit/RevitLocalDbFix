using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RevitLocalDbFix.Core.Infrastructure;

namespace RevitLocalDbFix.Core.AdvanceSteel
{
    /// <summary>
    /// Finds installed Advance Steel years from uninstall entries whose DisplayName looks like
    /// "Autodesk Advance Steel 2024" (language packs and updates of the same year count too).
    /// [待实测]: DisplayName format not yet verified on a machine with Advance Steel installed.
    /// </summary>
    public sealed class AdvanceSteelInstallationLocator
    {
        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        private static readonly Regex DisplayNamePattern =
            new Regex(@"^(Autodesk\s+)?Advance\s+Steel\s+(20\d{2})\b", RegexOptions.IgnoreCase);

        private readonly IRegistryReader _reg;

        public AdvanceSteelInstallationLocator() : this(new RegistryReader64()) { }

        public AdvanceSteelInstallationLocator(IRegistryReader reg)
        {
            _reg = reg ?? throw new ArgumentNullException("reg");
        }

        /// <summary>Distinct installed years, ascending.</summary>
        public IReadOnlyList<int> EnumerateYears()
        {
            var years = new SortedSet<int>();
            foreach (var root in UninstallRoots)
            {
                foreach (var sub in _reg.GetSubKeyNames(root))
                {
                    var displayName = _reg.GetValue(root + "\\" + sub, "DisplayName") as string;
                    if (displayName == null) continue;
                    var m = DisplayNamePattern.Match(displayName.Trim());
                    if (m.Success) years.Add(int.Parse(m.Groups[2].Value));
                }
            }
            return new List<int>(years);
        }
    }
}
