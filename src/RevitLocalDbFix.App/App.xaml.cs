using System;
using System.Windows;
using RevitLocalDbFix.App.Localization;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Infrastructure;

namespace RevitLocalDbFix.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Paths.EnsureToolDirs();
            FileLogger.Log("App start, version " + typeof(App).Assembly.GetName().Version);
            Loc.Initialize();

            bool resume = false;
            if (e.Args != null)
            {
                foreach (var arg in e.Args)
                {
                    if (string.Equals(arg, "--resume", StringComparison.OrdinalIgnoreCase)) resume = true;
                }
            }

            var window = new MainWindow(resume);
            MainWindow = window;
            window.Show();
        }
    }
}
