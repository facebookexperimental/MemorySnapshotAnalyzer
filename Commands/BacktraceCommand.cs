/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System.Collections.Generic;
using System.Text;

namespace MemorySnapshotAnalyzer.Commands
{
    public class BacktraceCommand : Command
    {
        public BacktraceCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [RemainingArguments]
        public List<NativeWord>? AddressOrIndexList;

        [FlagArgument("lifelines")]
        public bool Lifelines;

        [NamedArgument("ignoretagged")]
        public string? IgnoreIfAncestorHasTag;

        [NamedArgument("depth")]
        public int MaxDepth;

        [FlagArgument("fullyqualified")]
        public bool FullyQualified;

        [FlagArgument("fields")]
        public bool Fields = true;

        [NamedArgument("count")]
        public int MaxCount;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            if (!Lifelines && IgnoreIfAncestorHasTag != null)
            {
                throw new CommandException("'ignoretagged may only be given with 'lifelines");
            }

            int numberOutput = 0;

            // Use a single "seen" set, so that backtraces for objects past the first argument
            // can be abbreviated if the first argument's backtrace already contained any
            // subsequent objects.
            var ancestors = new HashSet<int>();
            var seen = new HashSet<int>();

            Output.BeginArray(Lifelines ? "lifelines" : "backtraces");

            foreach (NativeWord addressOrIndex in AddressOrIndexList!)
            {
                int postorderIndex = Context.ResolveToPostorderIndex(addressOrIndex);
                int nodeIndex = CurrentBacktracer.PostorderIndexToNodeIndex(postorderIndex);

                if (Lifelines)
                {
                    // DumpLifelines calls BeginElement/EndElement itself if necessary.
                    if (DumpLifelines(nodeIndex))
                    {
                        // Only count if we actually did output something.
                        numberOutput++;
                    }
                }
                else
                {
                    Output.BeginElement();
                    DumpBacktraces(nodeIndex, ancestors, seen, depth: 0, successorNodeIndex: -1);
                    Output.EndElement();

                    numberOutput++;
                }

                if (MaxCount > 0 && numberOutput >= MaxCount)
                {
                    break;
                }
            }

            Output.EndArray();

            Output.AddDisplayStringLine("found {0} {1}",
                numberOutput,
                Lifelines ? "lifeline(s)" : "backtrace(s)");
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
            bool first = true;
            foreach (int predIndex in CurrentBacktracer.Predecessors(nodeIndex))
            {
                if (first)
                {
                    Output.BeginArray("predecessors");
                    first = false;
                }

                Output.BeginElement();
                DumpBacktraces(predIndex, ancestors, seen, depth + 1, successorNodeIndex: nodeIndex);
                Output.EndElement();
            }
            ancestors.Remove(nodeIndex);

            if (!first)
            {
                Output.EndArray();
            }
        }

        protected bool DumpBacktraceLine(int nodeIndex, HashSet<int> ancestors, HashSet<int> seen, int indent, int successorNodeIndex, string prefix)
        {
            StringBuilder sb = new(prefix);

            bool result = seen.Contains(nodeIndex);
            if (result)
            {
                // Back reference to a node that was already printed.
                if (ancestors.Contains(nodeIndex))
                {
                    Output.AddProperty("seen", "upstack");
                    sb.Append("^^ ");
                }
                else
                {
                    Output.AddProperty("seen", "upAndOver");
                    sb.Append("~~ ");
                }
            }
            else
            {
                seen.Add(nodeIndex);

                AppendWeight(CurrentBacktracer.Weight(nodeIndex), sb);
            }

            sb.Append(CurrentBacktracer.DescribeNodeIndex(nodeIndex, Output, FullyQualified));

            if (CurrentBacktracer.IsLiveObjectNode(nodeIndex))
            {
                if (Fields)
                {
                    AppendFields(nodeIndex, successorNodeIndex, sb);
                }

                NativeWord address = CurrentTracedHeap.PostorderAddress(nodeIndex);
                AppendTags(address, sb);
            }

            Output.AddDisplayStringLineIndented(indent, sb.ToString());
            return result;
        }

        void AppendFields(int nodeIndex, int successorNodeIndex, StringBuilder sb)
        {
            if (successorNodeIndex != -1)
            {
                int postorderIndex = CurrentBacktracer.PostorderIndexToNodeIndex(nodeIndex);
                int successorPostorderIndex = CurrentBacktracer.PostorderIndexToNodeIndex(successorNodeIndex);
                AppendFields(postorderIndex, CurrentTracedHeap.PostorderAddress(successorPostorderIndex), sb);
            }
        }

        sealed class TrieNode
        {
            internal Dictionary<int, TrieNode>? Children;
        }

        bool DumpLifelines(int nodeIndex)
        {
            Dictionary<int, int[]> lifelines = ComputeLifelines(nodeIndex);
            if (lifelines.Count > 0)
            {
                TrieNode trie = CreateTrie(lifelines);

                Output.BeginElement();
                DumpTrie(nodeIndex, trie);
                Output.EndElement();

                return true;
            }
            else
            {
                return false;
            }
        }

        bool IsDestination(int nodeIndex)
        {
            return CurrentBacktracer.IsRootSentinel(nodeIndex) || CurrentBacktracer.Weight(nodeIndex) > 0;
        }

        bool ShouldIgnoreNode(int nodeIndex)
        {
            if (IgnoreIfAncestorHasTag != null)
            {
                // If a tag was given on the command line, stop if the object has that tag.
                int postorderIndex = CurrentBacktracer.NodeIndexToPostorderIndex(nodeIndex);
                return postorderIndex != -1 && CurrentTracedHeap.HasTag(postorderIndex, IgnoreIfAncestorHasTag);
            }
            else
            {
                return false;
            }
        }

        static TrieNode CreateTrie(Dictionary<int, int[]> lifelines)
        {
            TrieNode trie = new();
            foreach ((int _, int[] path) in lifelines)
            {
                TrieNode current = trie;
                for (int i = 1; i < path.Length; i++)
                {
                    current.Children ??= new Dictionary<int, TrieNode>();

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

        void DumpTrie(int nodeIndex, TrieNode trie)
        {
            HashSet<int> ancestors = new();
            HashSet<int> seen = new();
            DumpTrie(nodeIndex, trie, ancestors, seen, indent: 0, successorNodeIndex: -1, condensed: false, singleChild: true);
        }

        void DumpTrie(int nodeIndex, TrieNode trie, HashSet<int> ancestors, HashSet<int> seen, int indent, int successorNodeIndex, bool condensed, bool singleChild)
        {
            _ = DumpBacktraceLine(nodeIndex, ancestors, seen, indent, successorNodeIndex, prefix: condensed ? @"\ " : string.Empty);

            if (trie.Children != null)
            {
                Output.BeginArray("children");
                foreach ((int predNodeIndex, TrieNode child) in trie.Children)
                {
                    bool newCondense = singleChild && trie.Children.Count == 1;
                    int newIndent = newCondense ? indent : indent + 1;
                    Output.BeginElement();
                    DumpTrie(predNodeIndex, child, ancestors, seen, newIndent, nodeIndex, condensed: newCondense, singleChild: trie.Children.Count == 1);
                    Output.EndElement();
                }
                Output.EndArray();
            }
        }

        Dictionary<int, int[]> ComputeLifelines(int nodeIndex)
        {
            // This algorithm produces a cheap-to-compute approximation of "relative short" paths
            // from the target node to all of its transitive owners (strongly-owned nodes according
            // to the reference classifier, or roots). If a given object has been leaked (should have
            // become eligible for garbage collection, but hasn't), the lifelines provide (some)
            // relatively simple reference chains that need to be broken to make the object unreachable.

            List<int> currentPath = new();
            HashSet<int> seen = new();
            Dictionary<int, int[]> lifelines = new();
            ComputeLifelines(nodeIndex, currentPath, seen, lifelines);
            return lifelines;
        }

        void ComputeLifelines(int nodeIndex, List<int> currentPath, HashSet<int> seen, Dictionary<int, int[]> lifelines)
        {
            currentPath.Add(nodeIndex);

            if (!seen.Contains(nodeIndex))
            {
                seen.Add(nodeIndex);

                if (currentPath.Count > 1 && IsDestination(nodeIndex))
                {
                    // If we found a shorter lifeline to the same destination, only keep the shorter one.
                    if (!lifelines.TryGetValue(nodeIndex, out int[]? lifeline)
                        || lifeline.Length > currentPath.Count)
                    {
                        lifelines[nodeIndex] = currentPath.ToArray();
                    }
                }
                else
                {
                    foreach (int predNodeIndex in CurrentBacktracer.Predecessors(nodeIndex))
                    {
                        if (!ShouldIgnoreNode(predNodeIndex))
                        {
                            ComputeLifelines(predNodeIndex, currentPath, seen, lifelines);
                        }
                    }
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
        }

        public override string HelpText => "backtrace <object address or index> [['depth <max depth>] | 'lifelines ['ignoretagged <tag>] ['count <max>]] ['fullyqualified] ['fields]";
    }
}
