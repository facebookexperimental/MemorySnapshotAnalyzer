// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;

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
            Output.WriteLine($"total number of segments: {segmentedHeap.NumberOfSegments} ({numberOfObjectSegments} object, {numberOfRttiSegments} RTTI)");

            for (int i = 0; i < segmentedHeap.NumberOfSegments; i++)
            {
                HeapSegment segment = segmentedHeap.GetSegment(i);
                if (!RttiOnly || segment.IsRuntimeTypeInformation)
                {
                    Output.WriteLine("segment {0,6}: {1}",
                    i,
                    segment);
                }
            }
        }

        public override string HelpText => "listsegs ['rttionly]";
    }
}
