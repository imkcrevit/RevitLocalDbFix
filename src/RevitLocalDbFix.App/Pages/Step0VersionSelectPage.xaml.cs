using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitLocalDbFix.App.Localization;
using RevitLocalDbFix.Core.Elevation;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Revit;

namespace RevitLocalDbFix.App.Pages
{
    /// <summary>
    /// Step 0 (SPEC §5.0): lists installed Revit versions from the registry, shows system info
    /// and a read-only journal scan; manual Revit.exe fallback when nothing is detected.
    /// </summary>
    public partial class Step0VersionSelectPage : UserControl, IRelocalizable
    {
        private sealed class Row
        {
            public int Year { get; set; }
            public string Product { get; set; }
            public string Path { get; set; }
            public string ExeVersion { get; set; }
            public string Steel { get; set; }
            public string SteelKey { get; set; }
            public RevitInstallation Installation { get; set; }
        }

        public event EventHandler<RevitInstallation> VersionChosen;

        private readonly RevitInstallationLocator _locator = new RevitInstallationLocator();
        private readonly JournalScanner _journals = new JournalScanner();
        private readonly List<Row> _rows = new List<Row>();

        public Step0VersionSelectPage()
        {
            InitializeComponent();
            Relocalize();
            Refresh();
        }

        public void Relocalize()
        {
            ColYear.Header = Loc.T("Step0.ColYear");
            ColProduct.Header = Loc.T("Step0.ColProduct");
            ColPath.Header = Loc.T("Step0.ColPath");
            ColExeVersion.Header = Loc.T("Step0.ColExeVersion");
            ColSteel.Header = Loc.T("Step0.ColSteel");

            foreach (var row in _rows) row.Steel = Loc.T(row.SteelKey);
            Lv.Items.Refresh();

            UpdateSystemInfo();
            UpdateJournalInfo(SelectedRow());
        }

        private void Refresh()
        {
            _rows.Clear();
            try
            {
                foreach (var installation in _locator.Enumerate()) _rows.Add(ToRow(installation));
            }
            catch (Exception ex)
            {
                FileLogger.Log("Revit enumeration failed", ex);
            }

            Lv.ItemsSource = null;
            Lv.ItemsSource = _rows;
            BtnUse.IsEnabled = false;
            UpdateSystemInfo();
            UpdateJournalInfo(null);
        }

        private Row ToRow(RevitInstallation installation)
        {
            string steelKey = installation.SteelConnectionsExists
                ? "Step0.SteelYes"
                : (installation.DisabledSteelConnectionsPath != null ? "Step0.SteelDisabled" : "Step0.SteelNo");

            return new Row
            {
                Year = installation.Year,
                Product = installation.ProductName,
                Path = installation.InstallPath,
                ExeVersion = installation.ExeVersion ?? "?",
                SteelKey = steelKey,
                Steel = Loc.T(steelKey),
                Installation = installation
            };
        }

        private Row SelectedRow()
        {
            return Lv.SelectedItem as Row;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = SelectedRow();
            BtnUse.IsEnabled = row != null;
            UpdateJournalInfo(row);
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Revit.exe|Revit.exe|*.exe|*.exe",
                FileName = "Revit.exe"
            };
            if (dialog.ShowDialog() != true) return;

            var installation = _locator.FromExePath(dialog.FileName);
            if (installation == null)
            {
                MessageBox.Show(Window.GetWindow(this), Loc.T("Step0.ManualInvalid"),
                                Loc.T("App.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var row = ToRow(installation);
            _rows.RemoveAll(r => string.Equals(r.Path, row.Path, StringComparison.OrdinalIgnoreCase));
            _rows.Add(row);
            Lv.ItemsSource = null;
            Lv.ItemsSource = _rows;
            Lv.SelectedItem = row;
        }

        private void OnUse(object sender, RoutedEventArgs e)
        {
            var row = SelectedRow();
            if (row == null) return;
            var handler = VersionChosen;
            if (handler != null) handler(this, row.Installation);
        }

        private void UpdateSystemInfo()
        {
            var os = Environment.OSVersion.Version;
            bool isWin11 = os.Build >= 22000;
            TxtSysInfo.Text = string.Format(
                Loc.T("Step0.SysInfoFmt"),
                os.Major + "." + os.Minor,
                os.Build,
                isWin11 ? Loc.T("Step0.Win11Tag") : string.Empty,
                Environment.UserDomainName + "\\" + Environment.UserName,
                ElevationHelper.IsElevated() ? Loc.T("Common.Yes") : Loc.T("Common.No"));
        }

        private void UpdateJournalInfo(Row row)
        {
            if (_rows.Count == 0)
            {
                TxtJournal.Text = Loc.T("Step0.NoneFound");
                TxtJournal.Foreground = (Brush)FindResource("WarnBrush");
                return;
            }

            if (row == null)
            {
                TxtJournal.Text = string.Empty;
                return;
            }

            try
            {
                var hits = _journals.FindMalfunctionWarnings(row.Year);
                if (hits.Count == 0)
                {
                    TxtJournal.Text = Loc.T("Step0.JournalNone");
                    TxtJournal.Foreground = (Brush)FindResource("TextSecondaryBrush");
                }
                else
                {
                    var latest = hits.OrderByDescending(h => h.FileTime).First();
                    TxtJournal.Text = string.Format(
                        Loc.T("Step0.JournalHitsFmt"),
                        hits.Count,
                        Path.GetFileName(latest.FilePath),
                        latest.LineNumber);
                    TxtJournal.Foreground = (Brush)FindResource("WarnBrush");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log("Journal scan failed", ex);
                TxtJournal.Text = string.Empty;
            }
        }
    }
}
