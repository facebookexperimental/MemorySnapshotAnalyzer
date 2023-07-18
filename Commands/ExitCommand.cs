// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.CommandInfrastructure;
using System;

namespace MemorySnapshotAnalyzer.Commands
{
    public class ExitCommand : Command
    {
        public ExitCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value 0
        [PositionalArgument(0, optional: true)]
        public int Code;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value 0

        public override void Run()
        {
            Environment.Exit(Code);
        }

        public override string HelpText => "exit";
    }
}
