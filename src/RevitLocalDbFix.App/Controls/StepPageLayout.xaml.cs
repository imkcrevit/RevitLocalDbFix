using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitLocalDbFix.App.Localization;
using RevitLocalDbFix.App.Pages;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;
using RevitLocalDbFix.Core.Workflow;

namespace RevitLocalDbFix.App.Controls
{
    /// <summary>
    /// The uniform step page (SPEC §6.2): command preview, raw output vs expected output,
    /// verdict badge with reason, article link, and the five standard buttons.
    /// Step pages (M3/M4) embed this control and wire the events.
    /// </summary>
    public partial class StepPageLayout : UserControl, IRelocalizable
    {
        private string _articleLinkId;
        private Verdict _verdict = Verdict.NotRun;
        private string _verdictReason = string.Empty;

        public StepPageLayout()
        {
            InitializeComponent();
            ApplyVerdict();
        }

        public event EventHandler ExecuteClicked;
        public event EventHandler ConfirmClicked;
        public event EventHandler SkipClicked;
        public event EventHandler BackClicked;

        public string TitleText
        {
            get { return TxtTitle.Text; }
            set { TxtTitle.Text = value; }
        }

        public string CommandText
        {
            get { return TxtCommand.Text; }
            set { TxtCommand.Text = value; }
        }

        public string RawOutputText
        {
            get { return TxtRaw.Text; }
            set { TxtRaw.Text = value; }
        }

        public string ExpectedText
        {
            get { return TxtExpected.Text; }
            set { TxtExpected.Text = value; }
        }

        public string NoteText
        {
            get { return TxtNote.Text; }
            set
            {
                TxtNote.Text = value;
                TxtNote.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool CanContinue
        {
            get { return BtnContinue.IsEnabled; }
            set { BtnContinue.IsEnabled = value; }
        }

        public bool CanExecute
        {
            get { return BtnExecute.IsEnabled; }
            set { BtnExecute.IsEnabled = value; }
        }

        /// <summary>Shows the "依据/Reference" hyperlink for a Links id (SPEC §6.2).</summary>
        public void SetArticle(string linkId)
        {
            _articleLinkId = linkId;
            LinkRun.Text = Loc.T("StepPage.Reference") + Links.GetTitle(linkId);
        }

        public void SetVerdict(Verdict verdict, string reason)
        {
            _verdict = verdict;
            _verdictReason = reason ?? string.Empty;
            ApplyVerdict();
        }

        public void Relocalize()
        {
            if (_articleLinkId != null)
                LinkRun.Text = Loc.T("StepPage.Reference") + Links.GetTitle(_articleLinkId);
            ApplyVerdict();
        }

        private void ApplyVerdict()
        {
            TxtVerdict.Text = Loc.T("Verdict." + _verdict);
            TxtVerdictReason.Text = _verdictReason;

            string fg, bg;
            switch (_verdict)
            {
                case Verdict.Pass: fg = "PassBrush"; bg = "PassSubtleBrush"; break;
                case Verdict.Warn: fg = "WarnBrush"; bg = "WarnSubtleBrush"; break;
                case Verdict.Fail: fg = "FailBrush"; bg = "FailSubtleBrush"; break;
                case Verdict.Skipped: fg = "SkipBrush"; bg = "HoverBgBrush"; break;
                default: fg = "NotRunBrush"; bg = "HoverBgBrush"; break;
            }
            TxtVerdict.Foreground = (Brush)FindResource(fg);
            VerdictBadge.Background = (Brush)FindResource(bg);
        }

        private void OnArticleClick(object sender, RoutedEventArgs e)
        {
            if (_articleLinkId == null) return;
            string url = Links.GetUrl(_articleLinkId);
            if (url == null) return;
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                FileLogger.Log("Open article failed: " + url, ex);
            }
        }

        private void OnExecute(object sender, RoutedEventArgs e)
        {
            var handler = ExecuteClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnContinue(object sender, RoutedEventArgs e)
        {
            var handler = ConfirmClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnSkip(object sender, RoutedEventArgs e)
        {
            var handler = SkipClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnBack(object sender, RoutedEventArgs e)
        {
            var handler = BackClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtRaw.Text)) Clipboard.SetText(TxtRaw.Text);
            }
            catch
            {
                // clipboard can be locked by another process; non-fatal
            }
        }
    }
}
