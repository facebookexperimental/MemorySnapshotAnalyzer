/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

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
            Output.AddDisplayStringLine(string.Empty);
        }

        protected void HexDumpAsAddresses(MemoryView memoryView, NativeWord baseAddress)
        {
            Output.BeginArray("memoryContentsAsAddresses");

            var sb = new StringBuilder();
            long currentLineOffset = 0;
            while (currentLineOffset + CurrentMemorySnapshot.Native.Size <= memoryView.Size)
            {
                Output.BeginElement();

                DescribeAddress(baseAddress + currentLineOffset, sb);
                Output.AddDisplayStringLine(sb.ToString());
                sb.Clear();

                currentLineOffset += CurrentMemorySnapshot.Native.Size;

                Output.EndElement();
            }

            Output.EndArray();
        }

        public override string HelpText => "dump <address>";
    }
}
