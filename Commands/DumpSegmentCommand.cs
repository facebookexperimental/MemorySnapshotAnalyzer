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

            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            // Try to retrieve by address.
            HeapSegment? segment = segmentedHeap.GetSegmentForAddress(Address);
            if (segment == null && Address.Value <= int.MaxValue)
            {
                // Try to retrieve by index.
                segment = segmentedHeap.GetSegment((int)Address.Value);
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
