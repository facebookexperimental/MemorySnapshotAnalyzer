﻿using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.Analysis;
using MemorySnapshotAnalyzer.CommandProcessing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Commands
{
    public class HeapDomCommand : Command
    {
        sealed class TreeSizeComparer : IComparer<int>
        {
            readonly HeapDom m_heapDom;

            public TreeSizeComparer(HeapDom heapDom)
            {
                m_heapDom = heapDom;
            }

            int IComparer<int>.Compare(int x, int y)
            {
                return m_heapDom.TreeSize(y).CompareTo(m_heapDom.TreeSize(x));
            }
        }

        public HeapDomCommand(Repl repl) : base(repl) {}

#pragma warning disable CS0649 // Field '...' is never assigned to, and will always have its default value
        [PositionalArgument(0, optional: false)]
        public string? OutputFilename;

        [NamedArgument("relativeto")]
        public int OtherContextId = -1;

        [NamedArgument("depth")]
        public int MaxDepth;

        [NamedArgument("width")]
        public int MaxWidth;

        [NamedArgument("minsize")]
        public int MinSize;

        [FlagArgument("nonleaves")]
        public bool NonLeafNodesOnly;

        [FlagArgument("elideunchanged")]
        public bool ElideUnchangedSubtrees;

        [FlagArgument("nodetype")]
        public bool NodeTypes;
#pragma warning restore CS0649 // Field '...' is never assigned to, and will always have its default value

        Dictionary<int, bool>? m_diffTree;
        int m_numberOfNodesWritten;

        public override void Run()
        {
            Context.EnsureHeapDom();
            if (OtherContextId != -1)
            {
                Context? otherContext = Repl.GetContext(OtherContextId);
                if (otherContext == null || otherContext.CurrentMemorySnapshot == null)
                {
                    throw new CommandException("nonexistent context");
                }

                otherContext.EnsureTracedHeap();
                m_diffTree = new Dictionary<int, bool>();
                _ = ComputeDiffTree(CurrentHeapDom.RootNodeIndex, otherContext);
            }

            m_numberOfNodesWritten = 0;
            using (var fileOutput = new FileOutput(OutputFilename!))
            {
                RedirectOutputToFilename(fileOutput);
                Output.Write("data=");
                DumpTree();
                UnredirectOutput();
            }

            Output.WriteLine("wrote {0} nodes", m_numberOfNodesWritten);
        }

        bool ComputeDiffTree(int nodeIndex, Context previousContext)
        {
            bool hasDiffs;
            if (Context.CurrentBacktracer!.IsLiveObjectNode(nodeIndex))
            {
                // TODO: this only works with non-relocating garbage collectors.
                int objectIndex = CurrentBacktracer.NodeIndexToObjectIndex(nodeIndex);
                NativeWord objectAddress = CurrentTracedHeap.ObjectAddress(objectIndex);
                int previousObjectIndex = previousContext!.CurrentTracedHeap!.ObjectAddressToIndex(objectAddress);
                hasDiffs = previousObjectIndex == -1;
            }
            else
            {
                hasDiffs = false;
            }

            List<int>? children = CurrentHeapDom.GetChildren(nodeIndex);
            int numberOfChildren = children == null ? 0 : children.Count;
            for (int i = 0; i < numberOfChildren; i++)
            {
                hasDiffs |= ComputeDiffTree(children![i], previousContext);
            }
            m_diffTree!.Add(nodeIndex, hasDiffs);
            return hasDiffs;
        }

        void DumpTree()
        {
            var sizeComparer = new TreeSizeComparer(CurrentHeapDom);
            _ = DumpTree(CurrentHeapDom.RootNodeIndex, sizeComparer, false, 0, out _);
        }

        long DumpTree(int nodeIndex, IComparer<int> comparer, bool needComma, int depth, out int numberOfElidedNodes)
        {
            // TODO: better limiting functions:
            // - specifically omit a long tail of leaf nodes in high-width nodes
            // - omit tail of nodes whose size is only a fraction of the (undumped) parent tree size

            if (MaxDepth > 0 && depth >= MaxDepth)
            {
                numberOfElidedNodes = NumberOfNodesInTree(nodeIndex);
                return -1;
            }

            List<int>? children = CurrentHeapDom.GetChildren(nodeIndex);
            int numberOfChildren = children == null ? 0 : children.Count;

            if (NonLeafNodesOnly && numberOfChildren == 0)
            {
                numberOfElidedNodes = 1;
                return -1;
            }

            if (CurrentHeapDom.TreeSize(nodeIndex) < MinSize)
            {
                numberOfElidedNodes = NumberOfNodesInTree(nodeIndex);
                return -1;
            }

            m_numberOfNodesWritten++;
            if (needComma)
            {
                Output.Write(",");
            }
            Output.Write("{{\"name\":\"{0}\",", CurrentHeapDom.Backtracer.DescribeNodeIndex(nodeIndex, fullyQualified: true));

            // TODO: include assembly group (one of {mscorlib,System[.*]}/{Unity[.*],UnityEngine[.*],UnityEditor[.*]}/other)

            if (nodeIndex == CurrentHeapDom.RootNodeIndex)
            {
                Output.Write("\"filename\":{0},",
                    JsonConvert.ToString(CurrentMemorySnapshot.Filename));
                Output.Write("\"heapDomCommandLine\":{0},",
                    JsonConvert.ToString(Repl.CurrentCommandLine));
            }

            if (NodeTypes)
            {
                Output.Write("\"nodetype\":\"{0}\",", CurrentHeapDom.Backtracer.NodeType(nodeIndex));
            }

            bool elideChildren = false;
            if (m_diffTree != null)
            {
                Output.Write("\"diff\":\"{0}\",",
                    m_diffTree[nodeIndex] ? "different" : "same");
                elideChildren = !m_diffTree[nodeIndex];
            }

            if (numberOfChildren == 0)
            {
                Output.Write("\"value\":{0}", CurrentHeapDom.NodeSize(nodeIndex));
            }
            else if (elideChildren)
            {
                Output.Write("\"value\":{0}", CurrentHeapDom.TreeSize(nodeIndex));
            }
            else
            {
                DumpChildren(children!, CurrentHeapDom.TreeSize(nodeIndex), comparer, depth);
            }
            Output.Write("}");

            numberOfElidedNodes = 0;
            return CurrentHeapDom.TreeSize(nodeIndex);
        }

        void DumpChildren(List<int> children, long treeSize, IComparer<int> comparer, int depth)
        {
            Output.Write("\"children\":[");

            var sortedChildren = new int[children.Count];
            for (int i = 0; i < children.Count; i++)
            {
                sortedChildren[i] = children![i];
            }
            Array.Sort(sortedChildren, comparer);

            long sizeDumped = 0;
            bool needComma = false;
            int numberOfElidedNodes = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (MaxWidth > 0 && i >= MaxWidth)
                {
                    numberOfElidedNodes += children.Count - i;
                    break;
                }
                long childSize = DumpTree(sortedChildren[i], comparer, needComma, depth + 1, out int numberOfElidedNodesInChild);
                numberOfElidedNodes += numberOfElidedNodesInChild;
                sizeDumped += childSize;
                if (childSize >= 0)
                {
                    needComma = true;
                }
            }

            long intrinsicSize = treeSize - sizeDumped;
            if (intrinsicSize > 0)
            {
                if (needComma)
                {
                    Output.Write(",");
                }

                if (numberOfElidedNodes > 0)
                {
                    Output.Write("{{\"name\":\"elided+{0}\",", numberOfElidedNodes);
                    if (NodeTypes)
                    {
                        Output.Write("\"nodetype\":\"elided\",");
                    }
                }
                else
                {
                    Output.Write("{\"name\":\"intrinsic\",");
                    if (NodeTypes)
                    {
                        Output.Write("\"nodetype\":\"intrinsic\",");
                    }
                }
                Output.Write("\"value\":{0}}}", intrinsicSize);
            }

            Output.Write("]");
        }

        int NumberOfNodesInTree(int nodeIndex)
        {
            int numberOfNodes = 1;
            List<int>? children = CurrentHeapDom.GetChildren(nodeIndex);
            int numberOfChildren = children == null ? 0 : children.Count;
            for (int i = 0; i < numberOfChildren; i++)
            {
                numberOfNodes += NumberOfNodesInTree(children![i]);
            }
            return numberOfNodes;
        }

        public override string HelpText => "heapdom <output filename> ['relativeto <context id> ['elideunchanged]] ['depth <depth>] ['width <width>] ['minsize <node size in bytes>] ['nonleaves] ['nodetype]";
    }
}
