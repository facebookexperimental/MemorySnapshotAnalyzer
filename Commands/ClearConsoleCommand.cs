// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class ClearConsoleCommand : Command
    {
        public ClearConsoleCommand(Repl repl) : base(repl) { }

        public override void Run()
        {
            Output.Clear();
        }

        public override string HelpText => "cls";
    }
}
