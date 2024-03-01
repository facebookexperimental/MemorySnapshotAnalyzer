/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class FindCommand : Command
    {
        public FindCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public NativeWord NativeWord;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            Native native = CurrentMemorySnapshot.Native;

            Output.BeginArray("instances");

            long instancesFound = 0;
            StringBuilder sb = new();
            for (int i = 0; i < segmentedHeap.NumberOfSegments; i++)
            {
                HeapSegment segment = segmentedHeap.GetSegment(i);
                MemoryView memoryView = segment.MemoryView;
                long limit = memoryView.Size - native.Size;
                for (long offset = 0; offset <= limit; offset += native.Size)
                {
                    NativeWord value = memoryView.ReadNativeWord(offset, native);
                    if (value == NativeWord)
                    {
                        Output.BeginElement();
                        Output.AddProperty("address", (segment.StartAddress + offset).ToString());
                        segment.Describe(Output, sb);
                        Output.AddDisplayStringLine("{0} : {1}", segment.StartAddress + offset, sb.ToString());
                        sb.Clear();
                        instancesFound++;
                        Output.EndElement();
                    }
                }
            }

            Output.EndArray();

            Output.AddDisplayStringLine(string.Empty);
            Output.AddProperty("totalNumberOfInstances", instancesFound);
            Output.AddDisplayStringLine("total instances found = {0}", instancesFound);
        }

        public override string HelpText => "find <pointer-sized value>";
    }
}
