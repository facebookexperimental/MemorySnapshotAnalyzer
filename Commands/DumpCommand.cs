// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Text;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpCommand : Command
    {
        public DumpCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public NativeWord Address;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            // TOOD: support format argument (words/symbols)

            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            MemoryView memoryView = segmentedHeap.GetMemoryViewForAddress(Address);
            if (!memoryView.IsValid)
            {
                throw new CommandException($"address {Address} not in mapped memory");
            }
            HexDumpAsAddresses(memoryView, Address);
            Output.WriteLine();
        }

        protected void HexDump(MemoryView memoryView, NativeWord baseAddress, int width)
        {
            long currentLineOffset = 0;
            while (currentLineOffset < memoryView.Size)
            {
                var sb = new StringBuilder();
                sb.AppendFormat("{0}:", baseAddress + currentLineOffset);
                for (int i = 0; i < width; i++)
                {
                    if (currentLineOffset + (i + 1) * CurrentMemorySnapshot.Native.Size > memoryView.Size)
                    {
                        return;
                    }

                    NativeWord value = memoryView.ReadNativeWord(currentLineOffset + i * CurrentMemorySnapshot.Native.Size, CurrentMemorySnapshot.Native);
                    sb.AppendFormat(" {0}", value);
                }

                Output.WriteLine(sb.ToString());

                currentLineOffset += width * CurrentMemorySnapshot.Native.Size;
            }
        }

        protected void HexDumpAsAddresses(MemoryView memoryView, NativeWord baseAddress)
        {
            var sb = new StringBuilder();
            long currentLineOffset = 0;
            while (currentLineOffset + CurrentMemorySnapshot.Native.Size <= memoryView.Size)
            {
                DescribeAddress(baseAddress + currentLineOffset, sb);
                Output.WriteLine(sb.ToString());
                sb.Clear();

                currentLineOffset += CurrentMemorySnapshot.Native.Size;
            }
        }

        public override string HelpText => "dump <address>";
    }
}
