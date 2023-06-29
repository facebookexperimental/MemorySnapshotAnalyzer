// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System.Collections.Generic;
using System.Text;

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

        [FlagArgument("frag")]
        public bool Fragmentation;
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

            if (Fragmentation)
            {
                DumpFragmentation();
                Output.WriteLine();
            }
        }

        void DumpTypeSystemStatistics()
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;
            foreach (string s in typeSystem.DumpStats())
            {
                Output.WriteLine(s);
            }
        }

        void DumpHeapStatistics()
        {
            // TODO: stats on gaps between segments, and identification of active heap

            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            var histogram = new SortedDictionary<long, int>();
            var objectSegmentHistogram = new SortedDictionary<long, int>();
            var rttiSegmentHistogram = new SortedDictionary<long, int>();
            long totalSize = 0;
            for (int i = 0; i < segmentedHeap.NumberOfSegments; i++)
            {
                HeapSegment segment = segmentedHeap.GetSegment(i);
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

        static void Tally(SortedDictionary<long, int> histogram, long size)
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

        void DumpFragmentation()
        {
            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            var heapMap = new Dictionary<ulong, SortedDictionary<ulong, int>>();
            for (int i = 0; i < segmentedHeap.NumberOfSegments; i++)
            {
                HeapSegment segment = segmentedHeap.GetSegment(i);
                heapMap.Add(segment.StartAddress.Value, new SortedDictionary<ulong, int>());
            }

            int numberOfPostorderNodes = CurrentTracedHeap.NumberOfPostorderNodes;
            for (int postorderIndex = 0; postorderIndex < numberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex != -1)
                {
                    NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                    int objectSize = CurrentTraceableHeap.GetObjectSize(address, typeIndex, committedOnly: false);

                    HeapSegment? segment = segmentedHeap.GetSegmentForAddress(address);
                    if (segment != null)
                    {
                        SortedDictionary<ulong, int> objectMap = heapMap[segment.StartAddress.Value];
                        objectMap.Add(address.Value, objectSize);
                    }
                }
            }

            var segmentStats = new SortedDictionary<ulong, (ulong largestGap, ulong totalObjectSize, ulong totalFreeSize)>();
            ulong totalHeapSize = 0;
            ulong totalUsedSize = 0;
            ulong totalFreeSize = 0;
            for (int i = 0; i < segmentedHeap.NumberOfSegments; i++)
            {
                HeapSegment segment = segmentedHeap.GetSegment(i);
                (ulong largestGap, ulong totalObjectSize, ulong totalFreeSize) tuple = ComputeUsageForSegment(segment, heapMap[segment.StartAddress.Value]);
                totalHeapSize += (ulong)segment.Size;
                totalUsedSize += tuple.totalObjectSize;
                totalFreeSize += tuple.totalFreeSize;
                segmentStats.Add(segment.StartAddress.Value, tuple);
            }

            Output.WriteLine("total heap size = {0}, used size = {1}, free size = {2}",
                totalHeapSize,
                totalUsedSize,
                totalFreeSize);

            foreach (KeyValuePair<ulong, (ulong largestGap, ulong totalObjectSize, ulong totalFreeSize)> kvp in segmentStats)
            {
                HeapSegment segment = segmentedHeap.GetSegmentForAddress(CurrentMemorySnapshot.Native.From(kvp.Key))!;
                Output.WriteLine("{0}: largest gap {1}, total object size {2}, total free size {3}",
                    segment,
                    kvp.Value.largestGap,
                    kvp.Value.totalObjectSize,
                    kvp.Value.totalFreeSize);
            }
        }

        (ulong largestGap, ulong totalObjectSize, ulong totalFreeSize) ComputeUsageForSegment(HeapSegment segment, SortedDictionary<ulong, int> objectMap)
        {
            ulong largestGap = 0;
            ulong totalObjectSize = 0;
            ulong totalFreeSize = 0;

            ulong previousObjectAddress = segment.StartAddress.Value;
            foreach (KeyValuePair<ulong, int> kvp in objectMap)
            {
                ulong delta = kvp.Key - previousObjectAddress;
                if (delta != 0)
                {
                    if (delta > largestGap)
                    {
                        largestGap = delta;
                    }

                    totalFreeSize += delta;
                }

                totalObjectSize += RoundUp((ulong)kvp.Value);

                previousObjectAddress = kvp.Key + (ulong)kvp.Value;
            }

            totalFreeSize += segment.EndAddress.Value - previousObjectAddress;

            return (largestGap, totalObjectSize, totalFreeSize);
        }

        ulong RoundUp(ulong size)
        {
            ulong nativeWordSize = (ulong)CurrentMemorySnapshot.Native.Size;
            ulong granularity = nativeWordSize * 2;
            return (size + granularity - 1) & ~(granularity - 1);
        }

        public override string HelpText => "stats ['types] ['heap] ['frag]";
    }
}
