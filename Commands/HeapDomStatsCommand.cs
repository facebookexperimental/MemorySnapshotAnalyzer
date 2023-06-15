// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandProcessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class HeapDomStatsCommand : Command
    {
        public HeapDomStatsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [FlagArgument("list")]
        public bool List;

        [NamedArgument("type")]
        public int TypeIndex = -1;

        [FlagArgument("memory")]
        public bool Memory;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (List)
            {
                ListToplevelObjects();
            }
            else
            {
                DumpStats();
            }
        }

        void DumpStats()
        {
            List<int>? children = CurrentHeapDom.GetChildren(CurrentHeapDom.RootNodeIndex);

            var stats = new Dictionary<int, (int Count, long Size)>();
            int totalObjectCount = 0;
            int totalToplevelCount = 0;
            if (children != null)
            {
                totalToplevelCount = children.Count;
                for (int i = 0; i < children.Count; i++)
                {
                    int nodeIndex = children[i];
                    if (CurrentBacktracer.IsLiveObjectNode(nodeIndex))
                    {
                        totalObjectCount++;
                        int postorderIndex = CurrentBacktracer.NodeIndexToPostorderIndex(nodeIndex);
                        int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                        if (stats.TryGetValue(typeIndex, out (int Count, long Size) data))
                        {
                            stats[typeIndex] = (data.Count + 1, data.Size + CurrentHeapDom.TreeSize(nodeIndex));
                        }
                        else
                        {
                            stats[typeIndex] = (1, CurrentHeapDom.TreeSize(nodeIndex));
                        }
                    }
                }
            }

            Output.WriteLine("Number of nodes dominated only by root node: {0} objects out of {1} toplevel nodes",
                totalObjectCount,
                totalToplevelCount);

            KeyValuePair<int, (int Count, long Size)>[] statsArray = stats.ToArray();
            Array.Sort(statsArray, (a, b) => b.Value.Count.CompareTo(a.Value.Count));

            foreach (var kvp in statsArray)
            {
                int typeIndex = kvp.Key;
                int count = kvp.Value.Count;
                long totalSize = kvp.Value.Size;
                Output.WriteLine("Type {0} (index {1}): {2} instances, total {3} bytes",
                    CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                    typeIndex,
                    count,
                    totalSize);
            }
        }

        void ListToplevelObjects()
        {
            List<int>? children = CurrentHeapDom.GetChildren(CurrentHeapDom.RootNodeIndex);
            if (children != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < children.Count; i++)
                {
                    int nodeIndex = children[i];
                    if (CurrentBacktracer.IsLiveObjectNode(nodeIndex))
                    {
                        int postorderIndex = CurrentBacktracer.NodeIndexToPostorderIndex(nodeIndex);
                        int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);

                        if (TypeIndex != -1 && typeIndex == TypeIndex || TypeIndex == -1 && typeIndex != -1)
                        {
                            NativeWord address = CurrentTracedHeap.PostorderAddress(postorderIndex);
                            DescribeAddress(address, sb);
                            Output.WriteLine(sb.ToString());
                            sb.Clear();

                            if (Memory)
                            {
                                SegmentedHeap? segmentedHeap = CurrentSegmentedHeapOpt;
                                if (segmentedHeap != null)
                                {
                                    MemoryView objectView = segmentedHeap.GetMemoryViewForAddress(address);
                                    DumpObjectMemory(address, objectView);
                                }
                            }
                        }
                    }
                }
            }
        }

        public override string HelpText => "heapdomstats ['list ['type <type index>] ['memory]]";
    }
}
