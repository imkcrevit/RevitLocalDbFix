namespace RevitLocalDbFix.Core.Workflow
{
    /// <summary>Every page/step of the wizard (SPEC §5).</summary>
    public enum StepId
    {
        Step0_SelectVersion,
        Step1_Isolation,
        Step1_Feedback,
        Step2_0_Sector,
        Step2_1_InstanceList,
        Step2_2_InstanceInfo,
        Step2_3_StartStop,
        Step2_4_Recreate,
        Step2_5_Reinstall_Uninstall,
        Step2_5_Reinstall_CleanTemp,
        Step2_5_Reinstall_UserDirOld,
        Step2_5_Reinstall_ProgramDirOld,
        Step2_5_Reinstall_Download,
        Step2_5_Reinstall_Install,
        Step2_6_Connect,
        Step3_Restore,
        Step4_FinalVerify
    }
}
