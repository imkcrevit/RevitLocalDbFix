using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using RevitLocalDbFix.Core.Config;

namespace RevitLocalDbFix.App.Localization
{
    /// <summary>
    /// Runtime language switching (SPEC §2: zh-CN + en-US, default follows the OS, switchable).
    /// Strings live in Resources/Strings.en.xaml and Strings.zh.xaml; XAML uses DynamicResource
    /// so a swap of the merged dictionary re-renders live. Code-built strings listen to
    /// <see cref="LanguageChanged"/> and re-pull via <see cref="T"/>.
    /// </summary>
    public static class Loc
    {
        public const string English = "en-US";
        public const string Chinese = "zh-CN";

        /// <summary>Marker key present in both string dictionaries, used to find/replace them.</summary>
        private const string MarkerKey = "Loc.Marker";

        public static string Current { get; private set; } = English;

        public static event EventHandler LanguageChanged;

        public static bool IsChinese
        {
            get { return Current == Chinese; }
        }

        public static void Initialize()
        {
            string lang = null;
            try
            {
                if (File.Exists(Paths.UiLanguageFile))
                    lang = File.ReadAllText(Paths.UiLanguageFile).Trim();
            }
            catch { /* fall back to OS language */ }

            if (lang != English && lang != Chinese)
            {
                lang = CultureInfo.InstalledUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                    ? Chinese
                    : English;
            }

            Apply(lang, save: false);
        }

        public static void SetLanguage(string culture)
        {
            if (culture == Current) return;
            Apply(culture, save: true);
        }

        public static string T(string key)
        {
            var value = Application.Current.TryFindResource(key) as string;
            return value ?? "[" + key + "]";
        }

        private static void Apply(string culture, bool save)
        {
            Current = culture == Chinese ? Chinese : English;
            string file = Current == Chinese ? "Strings.zh.xaml" : "Strings.en.xaml";

            var dict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Resources/" + file, UriKind.Absolute)
            };

            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                if (merged[i].Contains(MarkerKey)) merged.RemoveAt(i);
            }
            merged.Add(dict);

            try
            {
                var ci = CultureInfo.GetCultureInfo(Current);
                Thread.CurrentThread.CurrentUICulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;
            }
            catch { /* keep going with the dictionary swap */ }

            if (save)
            {
                try
                {
                    Directory.CreateDirectory(Paths.ToolDataRoot);
                    File.WriteAllText(Paths.UiLanguageFile, Current);
                }
                catch { /* non-fatal */ }
            }

            var handler = LanguageChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }
    }
}
