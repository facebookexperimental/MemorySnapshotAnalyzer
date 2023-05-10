// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class HeapDom
    {
        struct SizeEntry
        {
            public long NodeSizeExcludingDescendants;
            public long NodeSizeIncludingDescendants;
        }

        readonly IBacktracer m_backtracer;
        readonly TracedHeap m_tracedHeap;
        readonly IRootSet m_rootSet;
        readonly ManagedHeap m_managedHeap;
        readonly bool m_weakGCHandles;
        readonly int m_rootNodeIndex;
        readonly Dictionary<int, List<int>> m_domTree;
        readonly int m_numberOfNonLeafNodes;
        readonly SizeEntry[] m_sizes;

        public HeapDom(IBacktracer backtracer, bool weakGCHandles)
        {
            m_backtracer = backtracer;
            m_tracedHeap = backtracer.TracedHeap;
            m_rootSet = m_tracedHeap.RootSet;
            m_managedHeap = m_rootSet.ManagedHeap;
            m_weakGCHandles = weakGCHandles;
            m_rootNodeIndex = m_backtracer.RootNodeIndex;

            m_domTree = BuildDomTree(out m_numberOfNonLeafNodes);

            m_sizes = new SizeEntry[m_rootNodeIndex + 1];
            ComputeSizes(m_rootNodeIndex);
        }

        public IBacktracer Backtracer => m_backtracer;

        public int RootNodeIndex => m_rootNodeIndex;

        public long NodeSize(int nodeIndex)
        {
            return m_sizes[nodeIndex].NodeSizeExcludingDescendants;
        }

        public long TreeSize(int nodeIndex)
        {
            return m_sizes[nodeIndex].NodeSizeIncludingDescendants;
        }

        public int NumberOfNonLeafNodes => m_numberOfNonLeafNodes;

        public List<int>? GetChildren(int nodeIndex)
        {
            m_domTree.TryGetValue(nodeIndex, out List<int>? children);
            return children;
        }

        Dictionary<int, List<int>> BuildDomTree(out int numberOfNonLeafNodes)
        {
            // Engineered algorithm from https://www.cs.rice.edu/~keith/EMBED/dom.pdf

            // Given that indices are in postorder, the root node is the node with the highest index.
            int numberOfNodes = m_backtracer.NumberOfNodes;
            var doms = new int[numberOfNodes];
            for (int i = 0; i < m_rootNodeIndex; i++)
            {
                doms[i] = -1;
            }
            doms[m_rootNodeIndex] = m_rootNodeIndex;

            bool changed = true;
            while (changed)
            {
                changed = false;
                // Note that Backtracer assigned node indices in postorder.
                for (int nodeIndex = m_rootNodeIndex - 1; nodeIndex >= 0; nodeIndex--)
                {
                    int newIdom = -1;
                    foreach (int predIndex in Predecessors(nodeIndex))
                    {
                        int predIdom = doms[predIndex];
                        if (predIdom != -1)
                        {
                            newIdom = newIdom == -1 ? predIndex : Intersect(predIdom, newIdom, doms);
                        }
                    }
                    if (doms[nodeIndex] != newIdom)
                    {
                        doms[nodeIndex] = newIdom;
                        changed = true;
                    }
                }
            }

            var domTree = new Dictionary<int, List<int>>();
            numberOfNonLeafNodes = 0;
            for (int nodeIndex = 0; nodeIndex < m_rootNodeIndex; nodeIndex++)
            {
                int parentNodeIndex = doms[nodeIndex];
                if (domTree.TryGetValue(parentNodeIndex, out List<int>? children))
                {
                    children!.Add(nodeIndex);
                }
                else
                {
                    domTree.Add(parentNodeIndex, new List<int>() { nodeIndex });
                    numberOfNonLeafNodes++;
                }
            }

            return domTree;
        }

        IEnumerable<int> Predecessors(int nodeIndex)
        {
            List<int> predecessors = m_backtracer.Predecessors(nodeIndex);

            bool foundNonGCHandle = false;
            if (m_weakGCHandles)
            {
                for (int i = 0; i < predecessors.Count; i++)
                {
                    if (!m_backtracer.IsGCHandle(predecessors[i]))
                    {
                        foundNonGCHandle = true;
                        break;
                    }
                }
            }

            for (int i = 0; i < predecessors.Count; i++)
            {
                // If m_weakGCHandles is false, return all predecessors.
                // Otherwise, if the only predecessors for this node are GCHandles, return those GCHandles.
                // Otherwise, skip the GCHandles.
                if (!foundNonGCHandle || !m_backtracer.IsGCHandle(predecessors[i]))
                {
                    yield return predecessors[i];
                }
            }
        }

        static int Intersect(int finger1, int finger2, int[] doms)
        {
            while (finger1 != finger2)
            {
                while (finger1 < finger2)
                {
                    finger1 = doms[finger1];
                }
                while (finger2 < finger1)
                {
                    finger2 = doms[finger2];
                }
            }
            return finger1;
        }

        long ComputeSizes(int nodeIndex)
        {
            long nodeSize = ComputeNodeSize(nodeIndex);
            long totalSize = nodeSize;
            List<int>? children = GetChildren(nodeIndex);
            if (children != null)
            {
                foreach (var childNodeIndex in children)
                {
                    totalSize += ComputeSizes(childNodeIndex);
                }
            }

            SizeEntry entry;
            entry.NodeSizeExcludingDescendants = nodeSize;
            entry.NodeSizeIncludingDescendants = totalSize;
            m_sizes[nodeIndex] = entry;

            return totalSize;
        }

        long ComputeNodeSize(int nodeIndex)
        {
            if (m_backtracer.IsLiveObjectNode(nodeIndex))
            {
                int typeIndex = m_tracedHeap.ObjectTypeIndex(nodeIndex);
                MemoryView objectView = m_managedHeap.GetMemoryViewForAddress(m_tracedHeap.ObjectAddress(nodeIndex));
                return m_managedHeap.GetObjectSize(objectView, typeIndex, committedOnly: true);
            }
            else
            {
                // We do not care about the size of roots.
                // TODO: It might be worth considering using committed size at the root node level, however.
                return 0;
            }
        }
    }
}
