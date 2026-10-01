using System;
using System.Collections.Generic;
using RevitLocalDbFix.Core.Config;
using RevitLocalDbFix.Core.Revit;

namespace RevitLocalDbFix.Core.Workflow
{
    /// <summary>One repair session; persisted to state.json so the tool can resume (SPEC §4, §10).</summary>
    public sealed class WizardSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public RevitInstallation Revit { get; set; }
        public ProductProfile Profile { get; set; }

        public WizardState State { get; set; } = WizardState.Idle;
        /// <summary>State to continue with after a reboot (--resume via RunOnce, SPEC §4).</summary>
        public WizardState NextStateAfterReboot { get; set; } = WizardState.Idle;
        /// <summary>1..6 while <see cref="State"/> is Step2_Reinstall (SPEC §5.2-2.5).</summary>
        public int ReinstallSubStep { get; set; }

        public List<StepResult> Steps { get; set; } = new List<StepResult>();

        public string BackupZipPath { get; set; }
        public string DisabledFolderPath { get; set; }

        /// <summary>User confirmed in step 1 that the problem disappears without SteelConnections.</summary>
        public bool IsolationConfirmedCause { get; set; }
        public bool UserNeedsSteelConnections { get; set; }

        public void RecordStep(StepResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            result.At = DateTime.Now;
            Steps.Add(result);
        }
    }
}
