/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandInfrastructure;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Commands
{
    public class DumpDomCommand : Command
    {
        public DumpDomCommand(Repl repl) : base(repl) { }

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [RemainingArguments]
        public List<NativeWord>? AddressOrIndexList;

        [NamedArgument("type")]
        public CommandLineArgument? TypeIndexOrPattern;

        [FlagArgument("includederived")]
        public bool IncludeDerived;

        [FlagArgument("fullyqualified")]
        public bool FullyQualified;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        public override void Run()
        {
            DumpDominators();
        }

        sealed class TrieNode
        {
            internal Dictionary<int, TrieNode>? Children;
        }

        void DumpDominators()
        {
            TrieNode trie = ComputeDominators();
            HeapDomSizes heapDomSizes = MakeHeapDomSizes(TypeIndexOrPattern, IncludeDerived);
            DumpDominatorTrie(CurrentHeapDom.RootNodeIndex, trie, heapDomSizes, indent: 0, condensed: false, singleChild: true);
        }

        TrieNode ComputeDominators()
        {
            TrieNode trie = new();
            List<int> pathToRoot = new();
            foreach (NativeWord addressOrIndex in AddressOrIndexList!)
            {
                int postorderIndex = Context.ResolveToPostorderIndex(addressOrIndex);

                int currentNodeIndex = postorderIndex;
                do
                {
                    pathToRoot.Add(currentNodeIndex);
                    currentNodeIndex = CurrentHeapDom.GetDominator(currentNodeIndex);
                }
                // Note that with a SingletonRootSet, we may be asked to dump the dominator tree for an unreachable node.
                while (currentNodeIndex != -1 && currentNodeIndex != CurrentHeapDom.RootNodeIndex);

                TrieNode current = trie;
                for (int i = pathToRoot.Count - 1; i >= 0; i--)
                {
                    current.Children ??= new Dictionary<int, TrieNode>();

                    int nodeIndex = pathToRoot[i];
                    if (!current.Children.TryGetValue(nodeIndex, out TrieNode? child))
                    {
                        child = new TrieNode();
                        current.Children.Add(nodeIndex, child);
                    }

                    current = child;
                }

                pathToRoot.Clear();
            }
            return trie;
        }

        void DumpDominatorTrie(int nodeIndex, TrieNode trie, HeapDomSizes heapDomSizes, int indent, bool condensed, bool singleChild)
        {
            long nodeSize = heapDomSizes.NodeSize(nodeIndex);
            long treeSize = heapDomSizes.TreeSize(nodeIndex);
            Output.AddProperty("nodeIndex", nodeIndex);
            Output.AddProperty("nodeSize", nodeSize);
            Output.AddProperty("treeSize", treeSize);
            Output.AddDisplayStringLineIndented(indent, "{0}{1} - exclusive size {2}, inclusive size {3}",
                condensed ? @"\ " : string.Empty,
                CurrentHeapDom.Backtracer.DescribeNodeIndex(nodeIndex, Output, FullyQualified),
                nodeSize,
                treeSize);

            if (trie.Children != null)
            {
                Output.BeginArray("children");
                foreach ((int childNodeIndex, TrieNode child) in trie.Children)
                {
                    bool newCondense = singleChild && trie.Children.Count == 1;
                    int newIndent = newCondense ? indent : indent + 1;
                    Output.BeginElement();
                    DumpDominatorTrie(childNodeIndex, child, heapDomSizes, newIndent, condensed: newCondense, singleChild: trie.Children.Count == 1);
                    Output.EndElement();
                }
                Output.EndArray();
            }
        }

        public override string HelpText => "dumpdom ['type [<type index or pattern>] ['includederived]] ['fullyqualified] <object address or index ...>";
    }
}
