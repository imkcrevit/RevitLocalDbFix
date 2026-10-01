using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using RevitLocalDbFix.App.Localization;
using RevitLocalDbFix.Core.Workflow;

namespace RevitLocalDbFix.App.Navigation
{
    /// <summary>One sidebar entry (step or sub-step) with a status glyph (SPEC §6.1).</summary>
    public sealed class NavItem : INotifyPropertyChanged
    {
        private Verdict _status = Verdict.NotRun;

        public NavItem(string key, string titleKey, bool isSubStep)
        {
            Key = key;
            TitleKey = titleKey;
            IsSubStep = isSubStep;
        }

        public string Key { get; }
        public string TitleKey { get; }
        public bool IsSubStep { get; }

        public string Title
        {
            get { return Loc.T(TitleKey); }
        }

        public Thickness Indent
        {
            get { return new Thickness(IsSubStep ? 18 : 0, 0, 0, 0); }
        }

        public Verdict Status
        {
            get { return _status; }
            set
            {
                if (_status == value) return;
                _status = value;
                Raise("Status");
                Raise("StatusGlyph");
                Raise("StatusBrush");
            }
        }

        public string StatusGlyph
        {
            get
            {
                switch (_status)
                {
                    case Verdict.Pass: return "✓";    // ✓
                    case Verdict.Warn: return "!";
                    case Verdict.Fail: return "✕";    // ✕
                    case Verdict.Skipped: return "–"; // –
                    default: return "○";              // ○
                }
            }
        }

        public Brush StatusBrush
        {
            get
            {
                string brushKey;
                switch (_status)
                {
                    case Verdict.Pass: brushKey = "PassBrush"; break;
                    case Verdict.Warn: brushKey = "WarnBrush"; break;
                    case Verdict.Fail: brushKey = "FailBrush"; break;
                    case Verdict.Skipped: brushKey = "SkipBrush"; break;
                    default: brushKey = "NotRunBrush"; break;
                }
                return Application.Current.TryFindResource(brushKey) as Brush ?? Brushes.Gray;
            }
        }

        public void RefreshLanguage()
        {
            Raise("Title");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
