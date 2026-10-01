namespace RevitLocalDbFix.Core.Workflow
{
    /// <summary>
    /// Wizard state machine states (SPEC §10). Reinstall sub-steps 2.5.1-2.5.6 are tracked
    /// by <see cref="WizardSession.ReinstallSubStep"/> while the state is Step2_Reinstall.
    /// </summary>
    public enum WizardState
    {
        Idle = 0,
        VersionSelected,
        IsolationPreflight,
        IsolationDone,
        AwaitingIsolationFeedback,
        Restoring,
        Step2_Sector,
        Step2_List,
        Step2_Info,
        Step2_StartStop,
        Step2_Recreate,
        Step2_Reinstall,
        Step2_Connect,
        FinalVerify,
        AwaitingReboot,
        Finished
    }
}
