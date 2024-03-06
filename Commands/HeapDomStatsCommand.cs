/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
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

        [NamedArgument("type")]
        public CommandLineArgument? TypeIndexOrPattern;

        [FlagArgument("includederived")]
        public bool IncludeDerived;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            DumpStats();
        }

        void DumpStats()
        {
            TypeSet? typeSet;
            if (TypeIndexOrPattern != null)
            {
                typeSet = TypeIndexOrPattern.ResolveTypeIndexOrPattern(Context, IncludeDerived);
            }
            else
            {
                typeSet = null;
            }

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
                        int postorderIndex = CurrentBacktracer.NodeIndexToPostorderIndex(nodeIndex);
                        int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                        if (typeSet != null && !typeSet.Contains(typeIndex))
                        {
                            continue;
                        }

                        long treeSize = CurrentHeapDom.TreeSize(nodeIndex);
                        totalSize += treeSize;
                        totalObjectCount++;

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

            Output.AddProperty("totalNumberOfObjects", totalObjectCount);
            Output.AddProperty("totalNumberOfToplevelNodes", totalToplevelCount);
            Output.AddProperty("totalSize", totalSize);
            Output.AddDisplayStringLine("Number of nodes dominated only by root node: {0} objects out of {1} toplevel nodes, for a total of {2} bytes",
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

            Output.BeginArray("toplevelNodes");
            foreach (var kvp in statsArray)
            {
                Output.BeginElement();
                int typeIndex = kvp.Key;
                int count = kvp.Value.Count;
                CurrentTraceableHeap.TypeSystem.OutputType(Output, "nodeType", typeIndex);
                Output.AddProperty("count", count);
                Output.AddProperty("totalSize", totalSizeByType[typeIndex]);
                Output.AddDisplayStringLine("Type {0}:{1} (index {2}): {3} instances, total {4} bytes",
                    CurrentTraceableHeap.TypeSystem.Assembly(typeIndex),
                    CurrentTraceableHeap.TypeSystem.QualifiedName(typeIndex),
                    typeIndex,
                    count,
                    totalSizeByType[typeIndex]);
                Output.EndElement();
            }
            Output.EndArray();
        }

        public override string HelpText => "heapdomstats ['bysize] ['type [<type index or pattern>] ['includederived]]";
    }
}
