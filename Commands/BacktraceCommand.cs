// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandProcessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class BacktraceCommand : Command
    {
        public BacktraceCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public NativeWord AddressOrIndex;

        [PositionalArgument(1, optional: true)]
        public string? OutputDotFilename;

        [NamedArgument("depth")]
        public int MaxDepth;

        [FlagArgument("shortestpaths")]
        public bool ShortestPaths;

        [FlagArgument("mostspecificroots")]
        public bool MostSpecificRoots;

        [FlagArgument("allroots")]
        public bool RootsOnly;

        [FlagArgument("dom")]
        public bool Dominators;

        [FlagArgument("stats")]
        public bool Statistics;

        [FlagArgument("fullyqualified")]
        public bool FullyQualified;

        [FlagArgument("fields")]
        public bool Fields = true;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        int m_numberOfNodesOutput;
        int m_numberOfEdgesOutput;

        public override void Run()
        {
            int postorderIndex = Context.ResolveToPostorderIndex(AddressOrIndex);
            int nodeIndex = CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex);

            if (ShortestPaths)
            {
                DumpShortestPathsToRoots(nodeIndex);
            }
            else if (MostSpecificRoots)
            {
                DumpSingleMostSpecificRoots(nodeIndex);
            }
            else if (RootsOnly)
            {
                Output.WriteLineIndented(0, CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified));
                foreach (int rootIndex in GetAllReachableRoots(nodeIndex))
                {
                    Output.WriteLineIndented(1, CurrentBacktracer.DescribeNodeIndex(rootIndex, FullyQualified));
                }
            }
            else if (Dominators)
            {
                DumpDominators(nodeIndex);
            }
            else if (OutputDotFilename != null)
            {
                using (var fileOutput = new FileOutput(OutputDotFilename))
                {
                    RedirectOutputToFilename(fileOutput);
                    DumpBacktracesToDot(nodeIndex);
                    UnredirectOutput();

                    Output.WriteLine("{0} nodes and {1} edges output",
                        m_numberOfNodesOutput,
                        m_numberOfEdgesOutput);
                }
            }
            else
            {
                var ancestors = new HashSet<int>();
                var seen = new HashSet<int>();
                DumpBacktraces(nodeIndex, ancestors, seen, depth: 0, successorNodeIndex: -1);
            }
        }

        void DumpBacktraces(int nodeIndex, HashSet<int> ancestors, HashSet<int> seen, int depth, int successorNodeIndex)
        {
            if (MaxDepth > 0 && depth > MaxDepth)
            {
                return;
            }

            string fields = "";
            if (Fields && successorNodeIndex != -1 && CurrentBacktracer.IsLiveObjectNode(nodeIndex))
            {
                fields = CollectFields(nodeIndex, successorNodeIndex);
            }

            if (seen.Contains(nodeIndex))
            {
                // Back reference to a node that was already printed.
                if (ancestors.Contains(nodeIndex))
                {
                    Output.WriteLineIndented(depth, "^^ {0}{1}", CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
                }
                else
                {
                    Output.WriteLineIndented(depth, "~~ {0}{1}", CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
                }
                return;
            }

            seen.Add(nodeIndex);

            if (CurrentBacktracer.IsOwned(nodeIndex))
            {
                Output.WriteLineIndented(depth, "** {0}{1}", CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
            }
            else if (CurrentBacktracer.IsWeak(nodeIndex))
            {
                Output.WriteLineIndented(depth, ".. {0}{1}", CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
            }
            else
            {
                Output.WriteLineIndented(depth, "{0}{1}", CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
            }

            ancestors.Add(nodeIndex);
            foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
            {
                DumpBacktraces(predIndex, ancestors, seen, depth + 1, successorNodeIndex: nodeIndex);
            }
            ancestors.Remove(nodeIndex);
        }

        string CollectFields(int nodeIndex, int successorNodeIndex)
        {
            var sb = new StringBuilder();
            NativeWord address = CurrentTracedHeap.PostorderAddress(nodeIndex);
            int typeIndex = CurrentTracedHeap.PostorderTypeIndexOrSentinel(nodeIndex);
            foreach (PointerInfo<NativeWord> pointerInfo in CurrentTraceableHeap.GetIntraHeapPointers(address, typeIndex))
            {
                if (pointerInfo.FieldNumber != -1 && CurrentTracedHeap.ObjectAddressToPostorderIndex(pointerInfo.Value) == successorNodeIndex)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(", ");
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                    sb.Append(CurrentTraceableHeap.TypeSystem.FieldName(pointerInfo.TypeIndex, pointerInfo.FieldNumber));
                }
            }
            return sb.ToString();
        }

        void DumpShortestPathsToRoots(int nodeIndex)
        {
            // TODO: as written, this doesn't technically find the shortest paths.
            // Perhaps we should find all reachable roots first, then use Diijstra's algorithm.
            // Left it here because it's still somewhat useful.

            var currentPath = new List<int>();
            var seen = new HashSet<int>();
            var shortestPaths = new Dictionary<int, int[]>();
            ComputeShortestPathsToRoots(nodeIndex, currentPath, seen, shortestPaths);

            int[] reachableRoots = shortestPaths.Keys.ToArray();
            Array.Sort(reachableRoots, (a, b) => shortestPaths[a].Length.CompareTo(shortestPaths[b].Length));

            if (Statistics)
            {
                foreach (int rootNodeIndex in reachableRoots)
                {
                    Output.WriteLine("{0}: {1}",
                        CurrentBacktracer.DescribeNodeIndex(rootNodeIndex, FullyQualified),
                        shortestPaths[rootNodeIndex].Length);
                }
            }
            else
            {
                foreach (int rootNodeIndex in reachableRoots)
                {
                    int[] path = shortestPaths[rootNodeIndex];
                    for (int i = path.Length - 1; i >= 0; i--)
                    {
                        int thisNodeIndex = path[i];
                        int successorNodeIndex = i > 0 ? path[i - 1] : -1;

                        string fields = "";
                        if (Fields && successorNodeIndex != -1 && CurrentBacktracer.IsLiveObjectNode(thisNodeIndex))
                        {
                            fields = CollectFields(thisNodeIndex, successorNodeIndex);
                        }

                        Output.WriteLineIndented(i == path.Length - 1 ? 0 : 1, "{0}{1}",
                            CurrentBacktracer.DescribeNodeIndex(thisNodeIndex, FullyQualified),
                            fields);
                    }
                }
            }
        }

        void ComputeShortestPathsToRoots(int nodeIndex, List<int> currentPath, HashSet<int> seen, Dictionary<int, int[]> shortestPaths)
        {
            currentPath.Add(nodeIndex);

            if (!seen.Contains(nodeIndex))
            {
                seen.Add(nodeIndex);

                if (CurrentBacktracer.IsRootSentinel(nodeIndex))
                {
                    if (!shortestPaths.TryGetValue(nodeIndex, out int[]? shortestPath)
                        || shortestPath.Length > currentPath.Count)
                    {
                        shortestPaths[nodeIndex] = currentPath.ToArray();
                    }
                }
                else
                {
                    foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
                    {
                        ComputeShortestPathsToRoots(predIndex, currentPath, seen, shortestPaths);
                    }
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
        }

        void DumpBacktracesToDot(int nodeIndex)
        {
            Output.WriteLine("digraph BT {");
            DumpBacktracesToDot(nodeIndex, -1, new HashSet<int>(), 0);
            Output.WriteLine("}");
        }

        void DumpBacktracesToDot(int nodeIndex, int optSuccessorNodeIndex, HashSet<int> seen, int depth)
        {
            if (optSuccessorNodeIndex != -1)
            {
                // declare the edge
                Output.WriteLine("  n{0} -> n{1}",
                    nodeIndex,
                    optSuccessorNodeIndex);
                m_numberOfEdgesOutput++;
            }

            if (!seen.Contains(nodeIndex))
            {
                // declare the node to give it a recognizable label
                Output.WriteLine("  n{0} [label=\"{1}\"]",
                    nodeIndex,
                    CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified));
                m_numberOfNodesOutput++;

                seen.Add(nodeIndex);

                if (MaxDepth <= 0 || depth < MaxDepth)
                {
                    foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
                    {
                        DumpBacktracesToDot(predIndex, nodeIndex, seen, depth + 1);
                    }
                }
            }
        }

        List<int> GetAllReachableRoots(int nodeIndex)
        {
            var seen = new HashSet<int>();
            var roots = new List<int>();
            FindReachableRoots(nodeIndex, seen, roots);
            return roots;
        }

        void FindReachableRoots(int nodeIndex, HashSet<int> seen, List<int> roots)
        {
            if (!seen.Contains(nodeIndex))
            {
                seen.Add(nodeIndex);

                if (CurrentBacktracer.IsRootSentinel(nodeIndex))
                {
                    roots.Add(nodeIndex);
                }
                else
                {
                    foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
                    {
                        FindReachableRoots(predIndex, seen, roots);
                    }
                }
            }
        }

        void DumpSingleMostSpecificRoots(int nodeIndex)
        {
            Output.WriteLineIndented(0, CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified));
            List<string> rootPath = SingleMostSpecificVirtualRoot(nodeIndex);
            var sb = new StringBuilder();
            foreach (string s in rootPath)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" -> ");
                }
                sb.Append(s);
            }
            Output.WriteLineIndented(1, sb.ToString());
        }

        List<string> SingleMostSpecificVirtualRoot(int nodeIndex)
        {
            var result = new List<string>();

            List<int> reachableRoots = GetAllReachableRoots(nodeIndex);

            int numberOfGCHandles = 0;

            IRootSet.StaticRootInfo theOne = default;
            bool allFromOneAssembly = true;
            bool allFromOneNamespace = true;
            bool allFromOneClass = true;

            for (int i = 0; i < reachableRoots.Count && allFromOneAssembly; i++)
            {
                List<(int rootIndex, PointerInfo<NativeWord> PointerInfo)> rootInfos = CurrentTracedHeap.PostorderRootIndices(CurrentBacktracer.NodeIndexToPostorderIndex(nodeIndex));
                foreach ((int rootIndex, _) in rootInfos)
                {
                    if (CurrentRootSet.IsGCHandle(rootIndex))
                    {
                        numberOfGCHandles++;
                        continue;
                    }

                    // If roots are a mix of GCHandles and statics, ignore the GCHandles.
                    IRootSet.StaticRootInfo info = CurrentRootSet.GetStaticRootInfo(rootIndex);
                    if (info.AssemblyName == null)
                    {
                        allFromOneAssembly = false;
                    }

                    if (allFromOneAssembly)
                    {
                        if (theOne.AssemblyName == null)
                        {
                            theOne.AssemblyName = info.AssemblyName!;
                        }
                        else if (info.AssemblyName != theOne.AssemblyName)
                        {
                            allFromOneAssembly = false;
                        }
                    }

                    if (allFromOneNamespace)
                    {
                        if (theOne.NamespaceName == null)
                        {
                            theOne.NamespaceName = info.NamespaceName!;
                        }
                        else if (info.NamespaceName != theOne.NamespaceName)
                        {
                            allFromOneNamespace = false;
                        }
                    }

                    if (allFromOneClass)
                    {
                        if (theOne.ClassName == null)
                        {
                            theOne.ClassName = info.ClassName!;
                        }
                        else if (info.ClassName != theOne.ClassName)
                        {
                            allFromOneClass = false;
                        }
                    }
                }
            }

            if (allFromOneAssembly && theOne.AssemblyName != null)
            {
                result.Add(theOne.AssemblyName);
                if (allFromOneNamespace && theOne.NamespaceName != null)
                {
                    result.Add(theOne.NamespaceName);
                    if (allFromOneClass && theOne.ClassName != null)
                    {
                        result.Add(theOne.ClassName);
                    }
                }
            }
            else if (numberOfGCHandles == reachableRoots.Count)
            {
                result.Add("GCHandles");
            }
            return result;
        }

        void DumpDominators(int nodeIndex)
        {
            int currentNodeIndex = nodeIndex;
            int i = 0;
            do
            {
                Output.WriteLineIndented(i, "{0} - exclusive size {1}, inclusive size {2}",
                    CurrentHeapDom.Backtracer.DescribeNodeIndex(currentNodeIndex, FullyQualified),
                    CurrentHeapDom.NodeSize(currentNodeIndex),
                    CurrentHeapDom.TreeSize(currentNodeIndex));
                currentNodeIndex = CurrentHeapDom.GetDominator(currentNodeIndex);
                i = 1;
            }
            // Note that with a SingletonRootSet, we may be asked to dump the dominator tree for an unreachable node.
            while (currentNodeIndex != -1 && currentNodeIndex != CurrentHeapDom.RootNodeIndex);
        }

        public override string HelpText => "backtrace <object address or index> [[<output dot filename>] ['depth <max depth>] ['fields] | 'shortestpaths ['stats] | 'mostspecificroots | 'allroots | 'dom] ['fullyqualified]";
    }
}
