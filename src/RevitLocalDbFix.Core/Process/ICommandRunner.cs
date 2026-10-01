using System;

namespace RevitLocalDbFix.Core.Process
{
    public interface ICommandRunner
    {
        CommandResult Run(string exePath, string arguments, TimeSpan? timeout = null);
    }
}
