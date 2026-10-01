using System;
using System.Collections.Generic;

namespace RevitLocalDbFix.Core.Workflow
{
    /// <summary>Everything recorded about one executed (or skipped) step (SPEC §7).</summary>
    public sealed class StepResult
    {
        public StepId Id { get; set; }
        public Verdict Verdict { get; set; }
        /// <summary>Full command line shown to the user, when the step ran a command.</summary>
        public string Command { get; set; }
        public string RawOutput { get; set; }
        /// <summary>Expected output quoted from the official article (SPEC §3.3).</summary>
        public string Expected { get; set; }
        public string Reason { get; set; }
        /// <summary>Link id from <see cref="Config.Links"/> naming the article this step is based on.</summary>
        public string ArticleLinkId { get; set; }
        public DateTime At { get; set; }
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }
}
