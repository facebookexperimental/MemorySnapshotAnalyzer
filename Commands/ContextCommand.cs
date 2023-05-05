// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class ContextCommand : Command
    {
        public ContextCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: true)]
        public int Id = -1;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (Id != -1)
            {
                Repl.SwitchToContext(Id);
            }
            Repl.DumpContexts();
        }

        public override string HelpText => "context [<id>]";
    }
}
