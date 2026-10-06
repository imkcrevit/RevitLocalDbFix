using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitLocalDbFix.App.Localization;
using RevitLocalDbFix.Core.AdvanceSteel;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Provisioning;
using RevitLocalDbFix.Core.Revit;

namespace RevitLocalDbFix.App.Pages
{
    /// <summary>
    /// Tool page: check the dedicated LocalDB instance of every installed Revit / Advance Steel
    /// version and add missing ones with a pinned version (ART_LOCALDB_INVESTIGATE).
    /// </summary>
    public partial class InstancesPage : UserControl, IRelocalizable
    {
        private sealed class Row
        {
            public InstancePlan Plan { get; set; }
            public string Product { get; set; }
            public string Instance { get; set; }
            public string Engine { get; set; }
            public string Status { get; set; }
            public Brush StatusBrush { get; set; }
            public string Version { get; set; }
            public string Note { get; set; }
        }

        private readonly InstanceProvisioner _provisioner = new InstanceProvisioner();
        private IReadOnlyList<InstancePlan> _plans = new List<InstancePlan>();
        private bool _busy;

        public InstancesPage()
        {
            InitializeComponent();
            Relocalize();
            Loaded += async (s, e) =>
            {
                if (_plans.Count == 0 && !_busy) await RefreshAsync();
            };
        }

        public void Relocalize()
        {
            TxtIntro.Text = string.Format(Loc.T("Inst.IntroFmt"), Environment.UserDomainName + "\\" + Environment.UserName);
            BindRows();
        }

        private static List<ProductProfile> DetectProfiles()
        {
            var profiles = new List<ProductProfile>();
            try
            {
                foreach (var revit in new RevitInstallationLocator().Enumerate())
                    profiles.Add(ProductProfiles.ForRevit(revit.Year));
            }
            catch (Exception ex)
            {
                FileLogger.Log("Revit enumeration failed", ex);
            }
            try
            {
                foreach (var year in new AdvanceSteelInstallationLocator().EnumerateYears())
                    if (year >= 2018) profiles.Add(ProductProfiles.ForAdvanceSteel(year));
            }
            catch (Exception ex)
            {
                FileLogger.Log("Advance Steel enumeration failed", ex);
            }
            return profiles.OrderBy(p => p.Product).ThenBy(p => p.Year).ToList();
        }

        private async Task RefreshAsync()
        {
            SetBusy(true);
            try
            {
                _plans = await Task.Run(() => _provisioner.InspectAll(DetectProfiles()));
            }
            catch (Exception ex)
            {
                FileLogger.Log("Instance inspection failed", ex);
                _plans = new List<InstancePlan>();
                AppendOutput(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
            BindRows();
        }

        private void BindRows()
        {
            var selectedInstance = (Lv.SelectedItem as Row)?.Instance;
            var rows = _plans.Select(ToRow).ToList();
            Lv.ItemsSource = rows;
            Lv.SelectedItem = rows.FirstOrDefault(r => r.Instance == selectedInstance);

            if (rows.Count == 0)
            {
                TxtSummary.Text = _busy ? string.Empty : Loc.T("Inst.NoProducts");
            }
            else
            {
                int healthy = _plans.Count(p => p.Status == InstanceStatus.Healthy);
                int missing = _plans.Count(p => p.Status == InstanceStatus.Missing);
                int repair = _plans.Count(p => p.Status == InstanceStatus.WrongVersion ||
                                               p.Status == InstanceStatus.Unreadable ||
                                               p.Status == InstanceStatus.EngineMissing);
                TxtSummary.Text = string.Format(Loc.T("Inst.SummaryFmt"), _plans.Count, healthy, missing, repair);
            }
            UpdateCreateButton();
        }

        private Row ToRow(InstancePlan plan)
        {
            string brushKey;
            switch (plan.Status)
            {
                case InstanceStatus.Healthy: brushKey = "PassBrush"; break;
                case InstanceStatus.Missing: brushKey = "WarnBrush"; break;
                case InstanceStatus.AutomaticInstance: brushKey = "SkipBrush"; break;
                default: brushKey = "FailBrush"; break;
            }

            return new Row
            {
                Plan = plan,
                Product = plan.Profile.GetDisplayName(),
                Instance = plan.Profile.InstanceName,
                Engine = plan.Profile.LocalDbMajorVersion,
                Status = Loc.T("Inst.Status." + plan.Status),
                StatusBrush = (Brush)FindResource(brushKey),
                Version = plan.CurrentVersion ?? "—",
                Note = plan.AlternateNameFound == null ? string.Empty : string.Format(Loc.T("Inst.AlternateFmt"), plan.AlternateNameFound)
            };
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCreateButton();
        }

        private void UpdateCreateButton()
        {
            var row = Lv.SelectedItem as Row;
            BtnCreate.IsEnabled = !_busy && row != null && row.Plan.CanCreate;
        }

        private async void OnRefresh(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void OnCreate(object sender, RoutedEventArgs e)
        {
            var row = Lv.SelectedItem as Row;
            if (row == null || !row.Plan.CanCreate) return;
            var profile = row.Plan.Profile;

            var answer = MessageBox.Show(
                Window.GetWindow(this),
                string.Format(Loc.T("Inst.ConfirmFmt"), profile.GetDisplayName(), row.Plan.CreateCommandPreview,
                              Environment.UserDomainName + "\\" + Environment.UserName),
                Loc.T("Inst.ConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            SetBusy(true);
            ProvisionResult result;
            try
            {
                Paths.EnsureToolDirs();
                result = await Task.Run(() => _provisioner.Create(profile, Paths.BackupsDir));
            }
            catch (Exception ex)
            {
                result = new ProvisionResult { InstanceName = profile.InstanceName, Error = ex.Message };
            }
            finally
            {
                SetBusy(false);
            }

            var sb = new StringBuilder();
            if (result.BackupZipPath != null) sb.AppendLine(string.Format(Loc.T("Inst.BackupFmt"), result.BackupZipPath));
            foreach (var cmd in result.Commands)
            {
                sb.AppendLine("> " + cmd.CommandLine);
                sb.AppendLine(cmd.CombinedOutput.TrimEnd());
            }
            sb.AppendLine(result.Success
                ? string.Format(Loc.T("Inst.DoneFmt"), result.InstanceName, result.CreatedVersion)
                : string.Format(Loc.T("Inst.FailedFmt"), result.InstanceName, result.Error));
            AppendOutput(sb.ToString());
            FileLogger.Log("Add instance " + result.InstanceName + ": success=" + result.Success + " " + (result.Error ?? result.CreatedVersion));

            await RefreshAsync();
        }

        private void AppendOutput(string text)
        {
            TxtOutput.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text.TrimEnd() + Environment.NewLine + Environment.NewLine);
            TxtOutput.ScrollToEnd();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            BtnRefresh.IsEnabled = !busy;
            TxtBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            UpdateCreateButton();
        }

        private void OnArticleClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Links.ART_LOCALDB_INVESTIGATE) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                FileLogger.Log("Open article failed", ex);
            }
        }
    }
}
