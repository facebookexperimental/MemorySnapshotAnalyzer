/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System.Collections.Generic;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpSegmentCommand : DumpCommand
    {
        public DumpSegmentCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [FlagArgument("objects")]
        public bool Objects;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

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

            if (Objects)
            {
                DumpObjects(segment);
            }
            else
            {
                HexDumpAsAddresses(segment.MemoryView, segment.StartAddress);
            }

            Output.WriteLine();
        }

        void DumpObjects(HeapSegment segment)
        {
            var objectMap = new SortedDictionary<ulong, int>();

            int numberOfPostorderNodes = CurrentTracedHeap.NumberOfPostorderNodes;
            for (int postorderIndex = 0; postorderIndex < numberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex != -1)
                {
                    NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                    if (address >= segment.StartAddress && address < segment.EndAddress)
                    {
                        int objectSize = CurrentTraceableHeap.GetObjectSize(address, typeIndex, committedOnly: false);
                        objectMap.Add(address.Value, objectSize);
                    }
                }
            }

            var sb = new StringBuilder();
            NativeWord previousAddress = segment.StartAddress;
            foreach (KeyValuePair<ulong, int> kvp in objectMap)
            {
                NativeWord objectAddress = CurrentMemorySnapshot.Native.From(kvp.Key);
                if (objectAddress != previousAddress)
                {
                    Output.WriteLine("Gap of size {0}", objectAddress.Value - previousAddress.Value);
                }

                DescribeAddress(objectAddress, sb);
                Output.WriteLine(sb.ToString());
                sb.Clear();

                previousAddress = objectAddress + kvp.Value;
            }
        }

        public override string HelpText => "dumpseg <address or index> ['objects]";
    }
}
