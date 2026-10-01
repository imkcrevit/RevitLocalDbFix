using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Report;
using RevitLocalDbFix.Core.Revit;
using RevitLocalDbFix.Core.Workflow;
using Xunit;

namespace RevitLocalDbFix.Core.Tests
{
    public class ReportBuilderTests
    {
        private static WizardSession SampleSession()
        {
            var session = new WizardSession
            {
                Revit = new RevitInstallation
                {
                    Year = 2024,
                    ProductName = "Autodesk Revit 2024",
                    InstallPath = @"C:\Program Files\Autodesk\Revit 2024\",
                    ExePath = @"C:\Program Files\Autodesk\Revit 2024\Revit.exe",
                    ExeVersion = "24.2.30.84"
                },
                Profile = ProductProfiles.ForRevit(2024),
                State = WizardState.Step2_List
            };
            session.RecordStep(new StepResult
            {
                Id = StepId.Step2_1_InstanceList,
                Verdict = Verdict.Pass,
                Command = "\"C:\\...\\SqlLocalDB.exe\" i",
                RawOutput = "SteelConnections2024v15",
                Expected = "SteelConnections2024v15",
                Reason = "Target instance found.",
                ArticleLinkId = "ART_LOCALDB_INVESTIGATE"
            });
            return session;
        }

        [Fact]
        public void BuildMarkdown_ContainsHeaderLinksAndCodeBlocks()
        {
            var markdown = new ReportBuilder().BuildMarkdown(SampleSession());

            Assert.Contains("# RevitLocalDbFix Report", markdown);
            Assert.Contains(Links.ART_REVIT_HANG, markdown);
            Assert.Contains(Links.ART_LOCALDB_INVESTIGATE, markdown);
            Assert.Contains("```", markdown);
            Assert.Contains("SteelConnections2024v15", markdown);
            Assert.Contains("2.1 Instance list", markdown);
            Assert.Contains("Pass", markdown);
        }

        [Fact]
        public void BuildJson_RoundTripsInstanceName()
        {
            var json = new ReportBuilder().BuildJson(SampleSession());

            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.Contains("SteelConnections2024v15", json);
        }
    }
}
