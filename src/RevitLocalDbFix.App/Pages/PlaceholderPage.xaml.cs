using System.Windows.Controls;
using RevitLocalDbFix.App.Localization;

namespace RevitLocalDbFix.App.Pages
{
    /// <summary>Temporary page for steps scheduled in later milestones (SPEC §12).</summary>
    public partial class PlaceholderPage : UserControl, IRelocalizable
    {
        private readonly string _titleKey;
        private readonly string _milestone;

        public PlaceholderPage(string titleKey, string milestone)
        {
            InitializeComponent();
            _titleKey = titleKey;
            _milestone = milestone;
            Relocalize();
        }

        public void Relocalize()
        {
            TxtTitle.Text = Loc.T(_titleKey);
            TxtInfo.Text = string.Format(Loc.T("Placeholder.PendingFmt"), _milestone);
        }
    }
}
