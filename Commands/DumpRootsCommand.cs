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
                (NativeWord address, PointerFlags pointerFlags) = rootSet.GetRoot(rootIndex);
                if (address.Value == 0)
                {
                    continue;
                }

                DescribeAddress(address, sb);
                if (pointerFlags == PointerFlags.None)
                {
                    Output.WriteLine("{0}: {1} -> {2}",
                        rootIndex,
                        rootSet.DescribeRoot(rootIndex, fullyQualified: true),
                        sb);
                }
                else
                {
                    Output.WriteLine("{0}: {1} -> {2} ({3})",
                        rootIndex,
                        rootSet.DescribeRoot(rootIndex, fullyQualified: true),
                        sb,
                        pointerFlags);
                }
                sb.Clear();
            }
        }

        public override string HelpText => "dumproots";
    }
}
