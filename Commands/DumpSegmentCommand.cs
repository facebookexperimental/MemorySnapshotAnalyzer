// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpSegmentCommand : DumpCommand
    {
        public DumpSegmentCommand(Repl repl) : base(repl) {}

        public override void Run()
        {
            // TOOD: support format argument (words/symbols)

            HeapSegment? segment = CurrentMemorySnapshot.SegmentedHeap.GetSegmentForAddress(Address);

            if (segment == null && Address.Value <= int.MaxValue)
            {
                segment = CurrentMemorySnapshot.SegmentedHeap.GetSegment((int)Address.Value);
            }

            if (segment == null)
            {
                throw new CommandException($"{Address} is not an address in, or an index of, a managed heap segment");
            }

            Output.WriteLine("{0}", segment);

            HexDumpAsAddresses(segment.MemoryView, segment.StartAddress);
            Output.WriteLine();
        }

        public override string HelpText => "dumpseg <address or index>";
    }
}
