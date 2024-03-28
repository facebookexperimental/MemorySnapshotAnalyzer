/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class HeapDomSizes
    {
        sealed class TreeSizeComparer : IComparer<int>
        {
            readonly HeapDomSizes m_heapDomSizes;

            public TreeSizeComparer(HeapDomSizes heapDomSizes)
            {
                m_heapDomSizes = heapDomSizes;
            }

            int IComparer<int>.Compare(int x, int y)
            {
                return m_heapDomSizes.TreeSize(y).CompareTo(m_heapDomSizes.TreeSize(x));
            }
        }

        struct SizeEntry
        {
            public long NodeSizeExcludingDescendants;
            public long NodeSizeIncludingDescendants;
        }

        readonly HeapDom m_heapDom;
        readonly IBacktracer m_backtracer;
        readonly TracedHeap m_tracedHeap;
        readonly TraceableHeap m_traceableHeap;
        readonly SizeEntry[] m_sizes;

        public HeapDomSizes(HeapDom heapDom, TypeSet? typeSet)
        {
            m_heapDom = heapDom;
            m_backtracer = heapDom.Backtracer;
            m_tracedHeap = m_backtracer.TracedHeap;
            m_traceableHeap = m_tracedHeap.RootSet.TraceableHeap;
            m_sizes = new SizeEntry[heapDom.RootNodeIndex + 1];
            ComputeSizes(heapDom.RootNodeIndex, typeSet);
        }

        public IBacktracer Backtracer => m_heapDom.Backtracer;

        public int RootNodeIndex => m_heapDom.RootNodeIndex;

        public long NodeSize(int nodeIndex)
        {
            return m_sizes[nodeIndex].NodeSizeExcludingDescendants;
        }

        public long TreeSize(int nodeIndex)
        {
            return m_sizes[nodeIndex].NodeSizeIncludingDescendants;
        }

        public int NumberOfNonLeafNodes => m_heapDom.NumberOfNonLeafNodes;

        public int GetDominator(int nodeIndex) => m_heapDom.GetDominator(nodeIndex);

        public List<int>? GetChildren(int nodeIndex)
        {
            return m_heapDom.GetChildren(nodeIndex);
        }

        public IComparer<int> MakeComparer()
        {
            return new TreeSizeComparer(this);
        }

        long ComputeSizes(int nodeIndex, TypeSet? typeSet)
        {
            (long nodeSize, bool selected) = ComputeNodeSize(nodeIndex, typeSet);
            TypeSet? downwardTypeSet = selected ? null : typeSet;
            long totalSize = nodeSize;
            List<int>? children = GetChildren(nodeIndex);
            if (children != null)
            {
                foreach (var childNodeIndex in children)
                {
                    totalSize += ComputeSizes(childNodeIndex, downwardTypeSet);
                }
            }

            SizeEntry entry;
            entry.NodeSizeExcludingDescendants = nodeSize;
            entry.NodeSizeIncludingDescendants = totalSize;
            m_sizes[nodeIndex] = entry;

            return totalSize;
        }

        (long nodeSize, bool selected) ComputeNodeSize(int nodeIndex, TypeSet? typeSet)
        {
            int postorderIndex = m_backtracer.NodeIndexToPostorderIndex(nodeIndex);
            if (postorderIndex != -1)
            {
                int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
                if (typeIndex != -1 && (typeSet == null || typeSet.Contains(typeIndex)))
                {
                    NativeWord address = m_tracedHeap.PostorderAddress(postorderIndex);
                    return (m_traceableHeap.GetObjectSize(address, typeIndex, committedOnly: true), true);
                }
            }

            return (0, false);
        }
    }
}
