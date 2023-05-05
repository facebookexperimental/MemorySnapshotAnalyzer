using System;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    public class CommandException : Exception
    {
        public CommandException(string message) : base(message) { }
    }
}
