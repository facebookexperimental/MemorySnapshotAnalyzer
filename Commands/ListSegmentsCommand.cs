using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;

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
            var managedHeap = CurrentMemorySnapshot.ManagedHeap;

            int numberOfObjectSegments = 0;
            int numberOfRttiSegments = 0;
            for (int i = 0; i < managedHeap.NumberOfSegments; i++)
            {
                ManagedHeapSegment segment = managedHeap.GetSegment(i);
                if (segment.IsRuntimeTypeInformation)
                {
                    numberOfRttiSegments++;
                }
                else
                {
                    numberOfObjectSegments++;
                }
            }
            Output.WriteLine($"total number of segments: {managedHeap.NumberOfSegments} ({numberOfObjectSegments} object, {numberOfRttiSegments} RTTI)");

            for (int i = 0; i < managedHeap.NumberOfSegments; i++)
            {
                ManagedHeapSegment segment = managedHeap.GetSegment(i);
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
