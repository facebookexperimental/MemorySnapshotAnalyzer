// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Commands
{
    public class StatsCommand : Command
    {
        public StatsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [FlagArgument("types")]
        public bool TypeSystemStatistics;

        [FlagArgument("heap")]
        public bool HeapStatistics;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            if (TypeSystemStatistics)
            {
                DumpTypeSystemStatistics();
                Output.WriteLine();
            }

            if (HeapStatistics)
            {
                DumpHeapStatistics();
                Output.WriteLine();
            }
        }

        void DumpTypeSystemStatistics()
        {
            ITypeSystem typeSystem = CurrentMemorySnapshot.ManagedHeap.TypeSystem;
            foreach (string s in typeSystem.DumpStats())
            {
                Output.WriteLine(s);
            }
        }

        void DumpHeapStatistics()
        {
            // TODO: stats on gaps between segments/within segments

            ManagedHeap managedHeap = CurrentMemorySnapshot.ManagedHeap;

            var histogram = new SortedDictionary<long, int>();
            var objectSegmentHistogram = new SortedDictionary<long, int>();
            var rttiSegmentHistogram = new SortedDictionary<long, int>();
            long totalSize = 0;
            for (int i = 0; i < managedHeap.NumberOfSegments; i++)
            {
                ManagedHeapSegment segment = managedHeap.GetSegment(i);
                totalSize += segment.Size;

                Tally(histogram, segment.Size);

                if (segment.IsRuntimeTypeInformation)
                {
                    Tally(rttiSegmentHistogram, segment.Size);
                }
                else
                {
                    Tally(objectSegmentHistogram, segment.Size);
                }
            }

            Output.WriteLine("total heap size {0}", totalSize);
            foreach (var kvp in histogram)
            {
                int objectCount;
                objectSegmentHistogram.TryGetValue(kvp.Key, out objectCount);
                int rttiCount;
                rttiSegmentHistogram.TryGetValue(kvp.Key, out rttiCount);
                Output.WriteLine("size {0,8}: total segments {1} (object {2}, rtti {3})", kvp.Key, kvp.Value, objectCount, rttiCount);
            }
        }

        void Tally(SortedDictionary<long, int> histogram, long size)
        {
            int count;
            if (histogram.TryGetValue(size, out count))
            {
                histogram[size] = count + 1;
            }
            else
            {
                histogram[size] = 1;
            }
        }

        public override string HelpText => "stats ['types] ['heap]";
    }
}
