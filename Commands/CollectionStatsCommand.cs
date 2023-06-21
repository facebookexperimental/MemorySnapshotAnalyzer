// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System.Collections.Generic;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class CollectionStatsCommand : Command
    {
        public CollectionStatsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value null
        [FlagArgument("arrays")]
        public bool Arrays;

        [FlagArgument("bysize")]
        public bool OrderBySize;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value null

        public override void Run()
        {
            SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
            if (segmentedHeap == null)
            {
                throw new CommandException("memory contents for active heap not available");
            }

            if (Arrays)
            {
                DumpArrayStats();
            }
        }

        void DumpArrayStats()
        {
            TypeSystem typeSystem = CurrentTraceableHeap.TypeSystem;

            var stats = new SortedDictionary<int, List<NativeWord>>(Comparer<int>.Create((int a, int b) => b.CompareTo(a)));

            int numberOfPostorderNodes = CurrentTracedHeap.NumberOfPostorderNodes;
            for (int postorderIndex = 0; postorderIndex < numberOfPostorderNodes; postorderIndex++)
            {
                int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex != -1 && typeSystem.IsArray(typeIndex))
                {
                    NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                    int objectSize = CurrentTraceableHeap.GetObjectSize(address, typeIndex, committedOnly: false);
                    MemoryView objectView = CurrentSegmentedHeapOpt!.GetMemoryViewForAddress(address);
                    int arraySize = CurrentSegmentedHeapOpt!.ReadArraySize(objectView);
                    int arrayElementSize = typeSystem.GetArrayElementSize(typeSystem.BaseOrElementTypeIndex(typeIndex));

                    int key = OrderBySize ? objectSize : arraySize;
                    if (stats.TryGetValue(key, out List<NativeWord>? instances))
                    {
                        instances.Add(address);
                    }
                    else
                    {
                        stats.Add(key, new List<NativeWord>() { address });
                    }
                }
            }

            var sb = new StringBuilder();
            foreach (KeyValuePair<int, List<NativeWord>> kvp in stats)
            {
                Output.WriteLine("{0} {1}:",
                    OrderBySize ? "Size" : "Capacity",
                    kvp.Key);
                foreach (NativeWord address in kvp.Value)
                {
                    DescribeAddress(address, sb);
                    Output.WriteLine(sb.ToString());
                    sb.Clear();
                }
            }
        }

        public override string HelpText => "collectionstats ['arrays] ['bysize]";
    }
}
