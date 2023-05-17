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
        [PositionalArgument(0, optional: true)]
        public NativeWord AddressOrIndex;

        [PositionalArgument(1, optional: true)]
        public string? OutputDotFilename;

        [NamedArgument("depth")]
        public int MaxDepth;

        [FlagArgument("shortestpaths")]
        public bool ShortestPaths;

        [FlagArgument("allroots")]
        public bool RootsOnly;

        [FlagArgument("mostspecificroots")]
        public bool MostSpecificRoots;

        [FlagArgument("stats")]
        public bool Statistics;

        [FlagArgument("fullyqualified")]
        public bool FullyQualified;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        int m_numberOfNodesOutput;
        int m_numberOfEdgesOutput;

        public override void Run()
        {
            if (AddressOrIndex.Size == 0)
            {
                DumpRootsForAllObjectsDominatedOnlyByProcessNode();
                return;
            }

            int objectIndex = ResolveToObjectIndex(AddressOrIndex);
            int nodeIndex = CurrentBacktracer.ObjectIndexToNodeIndex(objectIndex);
            DumpCore(nodeIndex);
        }

        void DumpRootsForAllObjectsDominatedOnlyByProcessNode()
        {
            List<int>? children = CurrentHeapDom.GetChildren(CurrentHeapDom.RootNodeIndex);
            if (children != null)
            {
                if (Statistics)
                {
                    var stats = new Dictionary<int, int>();
                    foreach (int nodeIndex in children)
                    {
                        if (CurrentBacktracer.IsLiveObjectNode(nodeIndex))
                        {
                            int objectIndex = CurrentBacktracer.NodeIndexToObjectIndex(nodeIndex);
                            int typeIndex = CurrentTracedHeap.ObjectTypeIndex(objectIndex);
                            if (stats.TryGetValue(typeIndex, out int count))
                            {
                                stats[typeIndex] = count + 1;
                            }
                            else
                            {
                                stats[typeIndex] = 1;
                            }
                        }
                    }

                    int[] keys = stats.Keys.ToArray();
                    Array.Sort(keys, (a, b) => stats[b].CompareTo(stats[a]));
                    foreach (int key in keys)
                    {
                        Output.WriteLine("{0}: {1}",
                            CurrentTraceableHeap.TypeSystem.QualifiedName(key),
                            stats[key]);
                    }
                }
                else
                {
                    foreach (int nodeIndex in children)
                    {
                        DumpCore(nodeIndex);
                    }
                }
            }
        }

        void DumpCore(int nodeIndex)
        {
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
                DumpBacktraces(nodeIndex, ancestors, seen, 0);
            }
        }

        void DumpBacktraces(int nodeIndex, HashSet<int> ancestors, HashSet<int> seen, int depth)
        {
            if (MaxDepth > 0 && depth == MaxDepth)
            {
                return;
            }

            if (seen.Contains(nodeIndex))
            {
                // Back reference to a node that was already printed.
                if (ancestors.Contains(nodeIndex))
                {
                    Output.WriteLineIndented(depth, "^^ {0}", CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified));
                }
                else
                {
                    Output.WriteLineIndented(depth, "~~ {0}", CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified));
                }
                return;
            }

            seen.Add(nodeIndex);

            Output.WriteLineIndented(depth, CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified));

            ancestors.Add(nodeIndex);
            foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
            {
                DumpBacktraces(predIndex, ancestors, seen, depth + 1);
            }
            ancestors.Remove(nodeIndex);
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
                    if (!CurrentBacktracer.IsGCHandle(rootNodeIndex))
                    {
                        int[] path = shortestPaths[rootNodeIndex];
                        for (int i = path.Length - 1; i >= 0; i--)
                        {
                            Output.WriteLineIndented(i == path.Length - 1 ? 0 : 1,
                                CurrentBacktracer.DescribeNodeIndex(path[i], FullyQualified));
                        }
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

                if (CurrentBacktracer.IsRootSetNode(nodeIndex))
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

                if (CurrentBacktracer.IsRootSetNode(nodeIndex))
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
                if (CurrentBacktracer.IsGCHandle(reachableRoots[i]))
                {
                    numberOfGCHandles++;
                }
                else
                {
                    // If roots are a mix of GCHandles and statics, ignore the GCHandles.
                    IRootSet.StaticRootInfo info = CurrentRootSet.GetStaticRootInfo(CurrentBacktracer.NodeIndexToRootIndex(reachableRoots[i]));
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

        public override string HelpText => "backtrace [<object address or index> [<output dot filename>] ['depth <max depth>]|['stats]] ['shortestpaths ['stats]|'mostspecificroots|'allroots]] ['fullyqualified]";
    }
}
