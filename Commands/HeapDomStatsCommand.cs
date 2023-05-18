// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.CommandProcessing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MemorySnapshotAnalyzer.Commands
{
    public class HeapDomStatsCommand : Command
    {
        public HeapDomStatsCommand(Repl repl) : base(repl) {}

        public override void Run()
        {
            int rootNodeIndex = CurrentHeapDom.RootNodeIndex;
            List<int>? children = CurrentHeapDom.GetChildren(CurrentHeapDom.RootNodeIndex);

            var stats = new Dictionary<int, Tuple<int, long>>();
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
                        if (stats.TryGetValue(typeIndex, out Tuple<int, long>? data))
                        {
                            stats[typeIndex] = Tuple.Create(data!.Item1 + 1, data!.Item2 + CurrentHeapDom.TreeSize(nodeIndex));
                        }
                        else
                        {
                            stats[typeIndex] = Tuple.Create(1, CurrentHeapDom.TreeSize(nodeIndex));
                        }
                    }
                }
            }

            Output.WriteLine("Number of nodes dominated only by root node: {0} objects out of {1} toplevel nodes",
                totalObjectCount,
                totalToplevelCount);

            var statsArray = stats.ToArray();
            Array.Sort(statsArray, (a, b) => b.Value.Item1.CompareTo(a.Value.Item1));

            foreach (var kvp in statsArray)
            {
                int typeIndex = kvp.Key;
                int count = kvp.Value.Item1;
                long totalSize = kvp.Value.Item2;
                Output.WriteLine("Type {0} (index {1}): {2} instances, total {3} bytes",
                    CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                    typeIndex,
                    count,
                    totalSize);
            }
        }

        public override string HelpText => "heapdomstats";
    }
}
