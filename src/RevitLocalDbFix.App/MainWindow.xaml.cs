using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitLocalDbFix.App.Localization;
using RevitLocalDbFix.App.Navigation;
using RevitLocalDbFix.App.Pages;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Elevation;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Revit;
using RevitLocalDbFix.Core.Workflow;

namespace RevitLocalDbFix.App
{
    public partial class MainWindow : Window
    {
        private readonly SessionStateStore _store = new SessionStateStore();
        private readonly ObservableCollection<NavItem> _navItems = new ObservableCollection<NavItem>();
        private readonly Dictionary<string, UserControl> _pages = new Dictionary<string, UserControl>();
        private WizardSession _session;

        public MainWindow(bool resume)
        {
            InitializeComponent();

            BuildNavigation();
            MiArticle1.Header = Links.TITLE_ART_REVIT_HANG;
            MiArticle2.Header = Links.TITLE_ART_LOCALDB_INVESTIGATE;

            Loc.LanguageChanged += OnLanguageChanged;
            Closed += (s, e) => Loc.LanguageChanged -= OnLanguageChanged;

            RefreshLanguageChecks();
            UpdateAdminUi();
            UpdateHeader();
            UpdateStatusBar();
            CheckExistingSession(resume);

            NavList.SelectedIndex = 0;
        }

        // ---------- navigation ----------

        private void BuildNavigation()
        {
            _navItems.Add(new NavItem("0", "Nav.Step0", false));
            _navItems.Add(new NavItem("1", "Nav.Step1", false));
            _navItems.Add(new NavItem("2", "Nav.Step2", false));
            _navItems.Add(new NavItem("2.0", "Nav.Step2_0", true));
            _navItems.Add(new NavItem("2.1", "Nav.Step2_1", true));
            _navItems.Add(new NavItem("2.2", "Nav.Step2_2", true));
            _navItems.Add(new NavItem("2.3", "Nav.Step2_3", true));
            _navItems.Add(new NavItem("2.4", "Nav.Step2_4", true));
            _navItems.Add(new NavItem("2.5", "Nav.Step2_5", true));
            _navItems.Add(new NavItem("2.6", "Nav.Step2_6", true));
            _navItems.Add(new NavItem("3", "Nav.Step3", false));
            _navItems.Add(new NavItem("4", "Nav.Step4", false));
            NavList.ItemsSource = _navItems;
        }

        private void OnNavSelected(object sender, SelectionChangedEventArgs e)
        {
            var item = NavList.SelectedItem as NavItem;
            if (item == null) return;
            PageHost.Content = GetPage(item.Key);
        }

        private UserControl GetPage(string key)
        {
            UserControl page;
            if (_pages.TryGetValue(key, out page)) return page;

            if (key == "0")
            {
                var step0 = new Step0VersionSelectPage();
                step0.VersionChosen += OnVersionChosen;
                page = step0;
            }
            else
            {
                page = new PlaceholderPage(TitleKeyFor(key), MilestoneFor(key));
            }

            _pages[key] = page;
            return page;
        }

        private static string TitleKeyFor(string key)
        {
            switch (key)
            {
                case "1": return "Nav.Step1";
                case "2": return "Nav.Step2";
                case "2.0": return "Nav.Step2_0";
                case "2.1": return "Nav.Step2_1";
                case "2.2": return "Nav.Step2_2";
                case "2.3": return "Nav.Step2_3";
                case "2.4": return "Nav.Step2_4";
                case "2.5": return "Nav.Step2_5";
                case "2.6": return "Nav.Step2_6";
                case "3": return "Nav.Step3";
                case "4": return "Nav.Step4";
                default: return "Nav.Step0";
            }
        }

        private static string MilestoneFor(string key)
        {
            if (key == "1") return "M2";
            if (key == "2.5") return "M4";
            return "M3";
        }

        // ---------- session ----------

        private void OnVersionChosen(object sender, RevitInstallation installation)
        {
            _session = new WizardSession
            {
                Revit = installation,
                Profile = ProductProfiles.ForRevit(installation.Year),
                State = WizardState.VersionSelected
            };
            _store.Save(_session);
            FileLogger.Log("Session " + _session.Id + ": selected Revit " + installation.Year +
                           " at " + installation.InstallPath);
            UpdateHeader();
            UpdateStatusBar();
        }

        private void CheckExistingSession(bool resume)
        {
            var existing = _store.Load();
            if (existing == null || existing.State == WizardState.Finished) return;

            bool adopt = resume;
            if (!adopt)
            {
                var answer = MessageBox.Show(this, Loc.T("Resume.Found"), Loc.T("Resume.Title"),
                                             MessageBoxButton.YesNo, MessageBoxImage.Question);
                adopt = answer == MessageBoxResult.Yes;
            }

            if (adopt)
            {
                _session = existing;
                FileLogger.Log("Session " + _session.Id + " resumed in state " + _session.State);
                UpdateHeader();
                UpdateStatusBar();
            }
        }

        // ---------- header / status ----------

        private void UpdateHeader()
        {
            if (_session == null || _session.Profile == null)
            {
                TxtSelection.Text = Loc.T("Header.NoSelection");
            }
            else
            {
                TxtSelection.Text = string.Format(
                    Loc.T("Header.SelectionFmt"),
                    _session.Revit.Year,
                    _session.Profile.InstanceName,
                    _session.Profile.LocalDbMajorVersion);
            }
        }

        private void UpdateAdminUi()
        {
            bool elevated = ElevationHelper.IsElevated();
            TxtAdmin.Text = elevated ? Loc.T("Header.AdminYes") : Loc.T("Header.AdminNo");
            AdminBadge.Background = (Brush)FindResource(elevated ? "PassSubtleBrush" : "WarnSubtleBrush");
            TxtAdmin.Foreground = (Brush)FindResource(elevated ? "PassBrush" : "WarnBrush");
            BtnElevate.Visibility = elevated ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateStatusBar()
        {
            TxtSession.Text = Loc.T("Status.SessionLabel") +
                              (_session == null ? "—" : _session.Id.ToString("N").Substring(0, 8));
            TxtLog.Text = Loc.T("Status.LogLabel") + FileLogger.CurrentLogFile;
        }

        // ---------- menu handlers ----------

        private void OnOpenBackups(object sender, RoutedEventArgs e)
        {
            Paths.EnsureToolDirs();
            try { Process.Start("explorer.exe", "\"" + Paths.BackupsDir + "\""); }
            catch (Exception ex) { FileLogger.Log("Open backups failed", ex); }
        }

        private void OnExit(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnLangEnglish(object sender, RoutedEventArgs e)
        {
            Loc.SetLanguage(Loc.English);
        }

        private void OnLangChinese(object sender, RoutedEventArgs e)
        {
            Loc.SetLanguage(Loc.Chinese);
        }

        private void OnOpenArticle1(object sender, RoutedEventArgs e)
        {
            OpenUrl(Links.ART_REVIT_HANG);
        }

        private void OnOpenArticle2(object sender, RoutedEventArgs e)
        {
            OpenUrl(Links.ART_LOCALDB_INVESTIGATE);
        }

        private void OnAbout(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, Loc.T("About.Text"), Loc.T("About.Title"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnElevate(object sender, RoutedEventArgs e)
        {
            try
            {
                ElevationHelper.RelaunchElevated("--resume");
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                // The user cancelled UAC: stay in the non-elevated session.
                FileLogger.Log("Elevation cancelled or failed", ex);
            }
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { FileLogger.Log("Open url failed: " + url, ex); }
        }

        // ---------- language ----------

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            foreach (var item in _navItems) item.RefreshLanguage();
            RefreshLanguageChecks();
            UpdateHeader();
            UpdateAdminUi();
            UpdateStatusBar();
            foreach (var page in _pages.Values)
            {
                var relocalizable = page as IRelocalizable;
                if (relocalizable != null) relocalizable.Relocalize();
            }
        }

        private void RefreshLanguageChecks()
        {
            MiLangEn.IsChecked = !Loc.IsChinese;
            MiLangZh.IsChecked = Loc.IsChinese;
        }
    }
}
