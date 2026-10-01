using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Elevation;
using RevitLocalDbFix.Core.Workflow;

namespace RevitLocalDbFix.Core.Report
{
    public sealed class ReportPaths
    {
        public string MarkdownPath { get; set; }
        public string JsonPath { get; set; }
    }

    /// <summary>
    /// Builds the Markdown + JSON report (SPEC §8). The Markdown mirrors the article structure
    /// so it can be pasted directly into an Autodesk support case.
    /// </summary>
    public sealed class ReportBuilder
    {
        public string BuildMarkdown(WizardSession session)
        {
            var sb = new StringBuilder();
            var toolVersion = Assembly.GetExecutingAssembly().GetName().Version;

            sb.AppendLine("# RevitLocalDbFix Report");
            sb.AppendLine();
            sb.AppendLine("- Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("- Tool version: " + toolVersion);
            sb.AppendLine("- Session: " + session.Id);
            sb.AppendLine("- System: " + Environment.OSVersion.VersionString +
                          " | Administrator: " + (ElevationHelper.IsElevated() ? "yes" : "no") +
                          " | User: " + Environment.UserName);

            if (session.Revit != null)
            {
                sb.AppendLine("- Revit: " + session.Revit.Year + " | " + session.Revit.InstallPath +
                              " | Revit.exe " + (session.Revit.ExeVersion ?? "?"));
            }
            if (session.Profile != null)
            {
                sb.AppendLine("- Target: LocalDB " + session.Profile.LocalDbMajorVersion +
                              " | instance `" + session.Profile.InstanceName + "`");
            }
            sb.AppendLine("- References: [" + Links.TITLE_ART_REVIT_HANG + "](" + Links.ART_REVIT_HANG + ") / [" +
                          Links.TITLE_ART_LOCALDB_INVESTIGATE + "](" + Links.ART_LOCALDB_INVESTIGATE + ")");
            sb.AppendLine();

            foreach (var step in session.Steps)
            {
                sb.AppendLine("## " + StepTitle(step.Id) + " — " + step.Verdict);
                sb.AppendLine();
                if (!string.IsNullOrEmpty(step.ArticleLinkId))
                {
                    string url = Links.GetUrl(step.ArticleLinkId);
                    if (url != null)
                        sb.AppendLine("Reference: [" + Links.GetTitle(step.ArticleLinkId) + "](" + url + ")");
                    sb.AppendLine();
                }
                if (!string.IsNullOrEmpty(step.Command))
                {
                    sb.AppendLine("Command:");
                    sb.AppendLine("```");
                    sb.AppendLine(step.Command);
                    sb.AppendLine("```");
                }
                if (!string.IsNullOrEmpty(step.RawOutput))
                {
                    sb.AppendLine("Raw output:");
                    sb.AppendLine("```");
                    sb.AppendLine(step.RawOutput.TrimEnd());
                    sb.AppendLine("```");
                }
                if (!string.IsNullOrEmpty(step.Expected))
                {
                    sb.AppendLine("Expected (from the article):");
                    sb.AppendLine("```");
                    sb.AppendLine(step.Expected.TrimEnd());
                    sb.AppendLine("```");
                }
                if (!string.IsNullOrEmpty(step.Reason))
                    sb.AppendLine("Verdict reason: " + step.Reason);
                sb.AppendLine();
            }

            sb.AppendLine("## Conclusion");
            sb.AppendLine();
            sb.AppendLine("- Final state: " + session.State);
            sb.AppendLine("- Isolation confirmed LocalDB as cause: " + (session.IsolationConfirmedCause ? "yes" : "no/unknown"));
            if (!string.IsNullOrEmpty(session.BackupZipPath))
                sb.AppendLine("- SteelConnections backup: " + session.BackupZipPath);

            return sb.ToString();
        }

        public string BuildJson(WizardSession session)
        {
            var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
            var serializer = new DataContractJsonSerializer(typeof(WizardSession), settings);
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, session);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public ReportPaths Save(WizardSession session, string directory)
        {
            Directory.CreateDirectory(directory);
            int year = session.Revit != null ? session.Revit.Year : 0;
            string baseName = "Report_" + year + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var paths = new ReportPaths
            {
                MarkdownPath = Path.Combine(directory, baseName + ".md"),
                JsonPath = Path.Combine(directory, baseName + ".json")
            };
            File.WriteAllText(paths.MarkdownPath, BuildMarkdown(session), Encoding.UTF8);
            File.WriteAllText(paths.JsonPath, BuildJson(session), Encoding.UTF8);
            return paths;
        }

        private static string StepTitle(StepId id)
        {
            switch (id)
            {
                case StepId.Step0_SelectVersion: return "Step 0 — Select Revit version";
                case StepId.Step1_Isolation: return "Step 1 — Isolation (backup + disable SteelConnections)";
                case StepId.Step1_Feedback: return "Step 1 — User feedback";
                case StepId.Step2_0_Sector: return "2.0 Disk sector check";
                case StepId.Step2_1_InstanceList: return "2.1 Instance list";
                case StepId.Step2_2_InstanceInfo: return "2.2 Instance details";
                case StepId.Step2_3_StartStop: return "2.3 Start/stop test";
                case StepId.Step2_4_Recreate: return "2.4 Delete and recreate instance";
                case StepId.Step2_5_Reinstall_Uninstall: return "2.5.1 Uninstall LocalDB";
                case StepId.Step2_5_Reinstall_CleanTemp: return "2.5.2 Clean %temp%";
                case StepId.Step2_5_Reinstall_UserDirOld: return "2.5.3 Rename user LocalDB folder to _OLD";
                case StepId.Step2_5_Reinstall_ProgramDirOld: return "2.5.4 Rename Program Files LocalDB folder to _OLD";
                case StepId.Step2_5_Reinstall_Download: return "2.5.5 Download installer";
                case StepId.Step2_5_Reinstall_Install: return "2.5.6 Install LocalDB";
                case StepId.Step2_6_Connect: return "2.6 Connection verification";
                case StepId.Step3_Restore: return "Step 3 — Restore SteelConnections";
                case StepId.Step4_FinalVerify: return "Step 4 — Final verification";
                default: return id.ToString();
            }
        }
    }
}
