// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandProcessing;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpRootsCommand : Command
    {
        public DumpRootsCommand(Repl repl) : base(repl) {}

        public override void Run()
        {
            var sb = new StringBuilder();
            IRootSet rootSet = CurrentRootSet;
            for (int rootIndex = 0; rootIndex < rootSet.NumberOfRoots; rootIndex++)
            {
                NativeWord address = rootSet.GetRoot(rootIndex);
                if (address.Value == 0)
                {
                    continue;
                }

                DescribeAddress(address, sb);
                Output.WriteLine("{0}: {1} -> {2}",
                    rootIndex,
                    rootSet.DescribeRoot(rootIndex, fullyQualified: true),
                    sb.ToString());
                sb.Clear();
            }
        }

        public override string HelpText => "dumproots";
    }
}
