// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
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

        [FlagArgument("shortestpaths")]
        public bool ShortestPaths;

        [FlagArgument("allroots")]
        public bool RootsOnly;

        [FlagArgument("dom")]
        public bool Dominators;

        [NamedArgument("depth")]
        public int MaxDepth;

        [FlagArgument("fullyqualified")]
        public bool FullyQualified;

        [FlagArgument("fields")]
        public bool Fields = true;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        int m_numberOfNodesOutput;
        int m_numberOfEdgesOutput;

        public override void Run()
        {
            int numberOfModes = 0;
            numberOfModes += ShortestPaths ? 1 : 0;
            numberOfModes += RootsOnly ? 1 : 0;
            numberOfModes += Dominators ? 1 : 0;
            numberOfModes += OutputDotFilename != null ? 1 : 0;
            if (numberOfModes > 1)
            {
                throw new CommandException("at most one of a dot filename, 'shortestpaths, 'allroots, or 'dom may be given");
            }

            int postorderIndex = Context.ResolveToPostorderIndex(AddressOrIndex);
            int nodeIndex = CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex);

            if (ShortestPaths || RootsOnly)
            {
                DumpShortestPathsToRoots(nodeIndex);
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

            if (DumpBacktraceLine(nodeIndex, ancestors, seen, depth, successorNodeIndex, prefix: string.Empty))
            {
                return;
            }

            ancestors.Add(nodeIndex);
            foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
            {
                DumpBacktraces(predIndex, ancestors, seen, depth + 1, successorNodeIndex: nodeIndex);
            }
            ancestors.Remove(nodeIndex);
        }

        bool DumpBacktraceLine(int nodeIndex, HashSet<int> ancestors, HashSet<int> seen, int indent, int successorNodeIndex, string prefix)
        {
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
                    Output.WriteLineIndented(indent, "{0}^^ {1}{2}", prefix, CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
                }
                else
                {
                    Output.WriteLineIndented(indent, "{0}~~ {1}{2}", prefix, CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
                }
                return true;
            }
            seen.Add(nodeIndex);

            if (CurrentBacktracer.IsOwned(nodeIndex))
            {
                Output.WriteLineIndented(indent, "{0}** {1}{2}", prefix, CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
            }
            else if (CurrentBacktracer.IsWeak(nodeIndex))
            {
                Output.WriteLineIndented(indent, "{0}.. {1}{2}", prefix, CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
            }
            else
            {
                Output.WriteLineIndented(indent, "{0}{1}{2}", prefix,CurrentBacktracer.DescribeNodeIndex(nodeIndex, FullyQualified), fields);
            }

            return false;
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

        sealed class TrieNode
        {
            internal Dictionary<int, TrieNode>? Children;
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

            if (RootsOnly)
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
                TrieNode trie = CreateTrie(shortestPaths);

                var ancestors = new HashSet<int>();
                seen.Clear();
                DumpTrie(nodeIndex, trie, ancestors, seen, indent: 0, successorNodeIndex: -1, condensed: false, singleChild: true);
            }
        }

        static TrieNode CreateTrie(Dictionary<int, int[]> shortestPaths)
        {
            TrieNode trie = new();
            foreach ((int _, int[] path) in shortestPaths)
            {
                TrieNode current = trie;
                for (int i = 1; i < path.Length; i++)
                {
                    if (current.Children == null)
                    {
                        current.Children = new Dictionary<int, TrieNode>();
                    }

                    int nodeIndex = path[i];
                    if (!current.Children.TryGetValue(nodeIndex, out TrieNode? child))
                    {
                        child = new TrieNode();
                        current.Children.Add(nodeIndex, child);
                    }

                    current = child;
                }
            }
            return trie;
        }

        void DumpTrie(int nodeIndex, TrieNode trie, HashSet<int> ancestors, HashSet<int> seen, int indent, int successorNodeIndex, bool condensed, bool singleChild)
        {
            _ = DumpBacktraceLine(nodeIndex, ancestors, seen, indent, successorNodeIndex, prefix: condensed ? @"\ " : string.Empty);

            if (trie.Children != null)
            {
                foreach ((int predNodeIndex, TrieNode child) in trie.Children)
                {
                    bool newCondense = singleChild && trie.Children.Count == 1;
                    int newIndent = newCondense ? indent : indent + 1;
                    DumpTrie(predNodeIndex, child, ancestors, seen, newIndent, nodeIndex, condensed: newCondense, singleChild: trie.Children.Count == 1);
                }
            }
        }

        void ComputeShortestPathsToRoots(int nodeIndex, List<int> currentPath, HashSet<int> seen, Dictionary<int, int[]> shortestPaths)
        {
            ComputeShortestPaths(nodeIndex, currentPath, seen, shortestPaths, nodeIndex => CurrentBacktracer.IsRootSentinel(nodeIndex));
        }

        void ComputeShortestPaths(int nodeIndex, List<int> currentPath, HashSet<int> seen, Dictionary<int, int[]> shortestPaths, Predicate<int> isDestination)
        {
            currentPath.Add(nodeIndex);

            if (!seen.Contains(nodeIndex))
            {
                seen.Add(nodeIndex);

                if (isDestination(nodeIndex))
                {
                    if (!shortestPaths.TryGetValue(nodeIndex, out int[]? shortestPath)
                        || shortestPath.Length > currentPath.Count)
                    {
                        shortestPaths[nodeIndex] = currentPath.ToArray();
                    }
                }
                else
                {
                    foreach (int predNodeIndex in CurrentBacktracer.Predecessors(nodeIndex))
                    {
                        ComputeShortestPaths(predNodeIndex, currentPath, seen, shortestPaths, isDestination);
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

        public override string HelpText => "backtrace <object address or index> [[<output dot filename>] ['depth <max depth>] ['fields] | 'shortestpaths ['stats] | 'allroots | 'dom] ['fullyqualified]";
    }
}
