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
    public class ListSegmentsCommand : Command
    {
        public ListSegmentsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [FlagArgument("rttionly")]
        public bool RttiOnly;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            int numberOfObjectSegments = 0;
            int numberOfRttiSegments = 0;
            for (int i = 0; i < segmentedHeap.NumberOfSegments; i++)
            {
                HeapSegment segment = segmentedHeap.GetSegment(i);
                if (segment.IsRuntimeTypeInformation)
                {
                    numberOfRttiSegments++;
                }
                else
                {
                    numberOfObjectSegments++;
                }
            }

            Output.AddProperty("totalNumberOfSegments", segmentedHeap.NumberOfSegments);
            Output.AddProperty("numberOfObjectSegments", numberOfObjectSegments);
            Output.AddProperty("numberOfRttiSegments", numberOfRttiSegments);
            Output.AddDisplayStringLine($"total number of segments: {segmentedHeap.NumberOfSegments} ({numberOfObjectSegments} object, {numberOfRttiSegments} RTTI)");

            Output.BeginArray("segments");
            StringBuilder sb = new();
            for (int i = 0; i < segmentedHeap.NumberOfSegments; i++)
            {
                HeapSegment segment = segmentedHeap.GetSegment(i);
                if (!RttiOnly || segment.IsRuntimeTypeInformation)
                {
                    Output.BeginElement();
                    Output.AddDisplayString("segment {0,6}: ", i);
                    segment.Describe(Output, sb);
                    Output.AddDisplayStringLine(sb.ToString());
                    sb.Clear();
                    Output.EndElement();
                }
            }
            Output.EndArray();
        }

        public override string HelpText => "listsegs ['rttionly]";
    }
}
