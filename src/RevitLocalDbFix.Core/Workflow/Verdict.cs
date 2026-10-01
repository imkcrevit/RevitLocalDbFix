namespace RevitLocalDbFix.Core.Workflow
{
    /// <summary>Per-step verdict (SPEC §7). Skipped is never treated as Pass (SPEC §9 rule 8).</summary>
    public enum Verdict
    {
        NotRun = 0,
        Pass,
        Warn,
        Fail,
        Skipped
    }
}
