// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Text;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DescribeCommand : Command
    {
        public DescribeCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public NativeWord Address;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            var sb = new StringBuilder();
            DescribeAddress(Address, sb);
            Output.WriteLine(sb.ToString());
        }

        public override string HelpText => "describe <address>";
    }
}
