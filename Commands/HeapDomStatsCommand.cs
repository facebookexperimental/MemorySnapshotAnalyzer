/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.CommandInfrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MemorySnapshotAnalyzer.Commands
{
    public class HeapDomStatsCommand : Command
    {
        public HeapDomStatsCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [FlagArgument("bysize")]
        public bool OrderBySize;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            DumpStats();
        }

        void DumpStats()
        {
            List<int>? children = CurrentHeapDom.GetChildren(CurrentHeapDom.RootNodeIndex);

            var stats = new Dictionary<int, (int Count, long Size)>();
            var totalSizeByType = new Dictionary<int, long>();
            int totalObjectCount = 0;
            int totalToplevelCount = 0;
            long totalSize = 0;
            if (children != null)
            {
                totalToplevelCount = children.Count;
                for (int i = 0; i < children.Count; i++)
                {
                    int nodeIndex = children[i];
                    if (CurrentBacktracer.IsLiveObjectNode(nodeIndex))
                    {
                        long treeSize = CurrentHeapDom.TreeSize(nodeIndex);
                        totalSize += treeSize;
                        totalObjectCount++;

                        int postorderIndex = CurrentBacktracer.NodeIndexToPostorderIndex(nodeIndex);
                        int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                        if (stats.TryGetValue(typeIndex, out (int Count, long Size) data))
                        {
                            stats[typeIndex] = (data.Count + 1, data.Size + CurrentHeapDom.TreeSize(nodeIndex));
                            totalSizeByType[typeIndex] += treeSize;
                        }
                        else
                        {
                            stats[typeIndex] = (1, CurrentHeapDom.TreeSize(nodeIndex));
                            totalSizeByType[typeIndex] = treeSize;
                        }
                    }
                }
            }

            Output.WriteLine("Number of nodes dominated only by root node: {0} objects out of {1} toplevel nodes, for a total of {2} bytes",
                totalObjectCount,
                totalToplevelCount,
                totalSize);

            KeyValuePair<int, (int Count, long Size)>[] statsArray = stats.ToArray();
            if (OrderBySize)
            {
                Array.Sort(statsArray, (a, b) => totalSizeByType[b.Key].CompareTo(totalSizeByType[a.Key]));
            }
            else
            {
                Array.Sort(statsArray, (a, b) => b.Value.Count.CompareTo(a.Value.Count));
            }

            foreach (var kvp in statsArray)
            {
                int typeIndex = kvp.Key;
                int count = kvp.Value.Count;
                Output.WriteLine("Type {0} (index {1}): {2} instances, total {3} bytes",
                    CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                    typeIndex,
                    count,
                    totalSizeByType[typeIndex]);
            }
        }

        public override string HelpText => "heapdomstats ['bysize]";
    }
}
