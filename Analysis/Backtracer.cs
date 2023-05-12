﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class Backtracer : IBacktracer
    {
        readonly ITracedHeap m_tracedHeap;
        readonly IRootSet m_rootSet;
        readonly TraceableHeap m_traceableHeap;
        readonly int m_rootNodeIndex;
        readonly Dictionary<int, List<int>> m_predecessors;

        public Backtracer(ITracedHeap tracedHeap)
        {
            m_tracedHeap = tracedHeap;
            m_rootSet = m_tracedHeap.RootSet;
            m_traceableHeap = m_rootSet.TraceableHeap;

            // For the purposes of backtracing, we assign node indices as follows:
            //   0 ... N-1 : nodes for live objects 0 ... N-1, in postorder
            //   N ... N+M-1 : nodes for root set indices 0 ... M-1
            //   N+M : root node - the containing process
            int numberOfLiveObjects = tracedHeap.NumberOfLiveObjects;
            int numberOfRoots = m_rootSet.NumberOfRoots;
            m_rootNodeIndex = numberOfLiveObjects + numberOfRoots;

            // TODO: use m_tracedHeap.GetNumberOfPredecessors for a more efficient representation
            m_predecessors = new Dictionary<int, List<int>>();
            ComputePredecessors();
        }

        public ITracedHeap TracedHeap => m_tracedHeap;

        public int RootNodeIndex => m_rootNodeIndex;

        public int NumberOfNodes => m_rootNodeIndex + 1;

        public bool IsLiveObjectNode(int nodeIndex)
        {
            return nodeIndex < m_tracedHeap.NumberOfLiveObjects;
        }

        public bool IsRootSetNode(int nodeIndex)
        {
            return nodeIndex >= m_tracedHeap.NumberOfLiveObjects && nodeIndex != m_rootNodeIndex;
        }

        public bool IsGCHandle(int nodeIndex)
        {
            return IsRootSetNode(nodeIndex) && m_rootSet.IsGCHandle(NodeIndexToRootIndex(nodeIndex));
        }

        public int NodeIndexToObjectIndex(int nodeIndex)
        {
            return nodeIndex;
        }

        public int NodeIndexToRootIndex(int nodeIndex)
        {
            return nodeIndex - m_tracedHeap.NumberOfLiveObjects;
        }

        public int ObjectIndexToNodeIndex(int objectIndex)
        {
            return objectIndex;
        }

        public string DescribeNodeIndex(int nodeIndex, bool fullyQualified)
        {
            if (nodeIndex == m_rootNodeIndex)
            {
                return "Process";
            }
            else if (nodeIndex < m_tracedHeap.NumberOfLiveObjects)
            {
                int objectIndex = NodeIndexToObjectIndex(nodeIndex);
                int typeIndex = m_tracedHeap.ObjectTypeIndex(objectIndex);
                return string.Format("{0}#{1}",
                    fullyQualified ?
                        m_traceableHeap.TypeSystem.QualifiedName(typeIndex) :
                        m_traceableHeap.TypeSystem.UnqualifiedName(typeIndex),
                    nodeIndex);
            }
            else
            {
                return m_rootSet.DescribeRoot(NodeIndexToRootIndex(nodeIndex), fullyQualified);
            }
        }

        public string NodeType(int nodeIndex)
        {
            if (nodeIndex == m_rootNodeIndex)
            {
                return "root";
            }
            else if (IsLiveObjectNode(nodeIndex))
            {
                int objectIndex = NodeIndexToObjectIndex(nodeIndex);
                int typeIndex = m_tracedHeap.ObjectTypeIndex(objectIndex);
                if (m_traceableHeap.TypeSystem.IsArray(typeIndex))
                {
                    return "array";
                }
                else if (m_traceableHeap.TypeSystem.IsValueType(typeIndex))
                {
                    return "box";
                }
                else
                {
                    return "object";
                }
            }
            else
            {
                return m_rootSet.RootType(NodeIndexToRootIndex(nodeIndex));
            }
        }

        public List<int> Predecessors(int nodeIndex)
        {
            return m_predecessors[nodeIndex];
        }

        void ComputePredecessors()
        {
            m_predecessors.Add(m_rootNodeIndex, new List<int>());

            for (int rootIndex = 0; rootIndex < m_rootSet.NumberOfRoots; rootIndex++)
            {
                AddPredecessor(m_tracedHeap.NumberOfLiveObjects + rootIndex, m_rootNodeIndex);

                NativeWord address = m_rootSet.GetRoot(rootIndex);
                int objectIndex = m_tracedHeap.ObjectAddressToIndex(address);
                if (objectIndex != -1)
                {
                    AddPredecessor(objectIndex, m_tracedHeap.NumberOfLiveObjects + rootIndex);
                }
            }

            for (int parentObjectIndex = 0; parentObjectIndex < m_tracedHeap.NumberOfLiveObjects; parentObjectIndex++)
            {
                NativeWord address = m_tracedHeap.ObjectAddress(parentObjectIndex);
                int typeIndex = m_tracedHeap.ObjectTypeIndex(parentObjectIndex);
                foreach (NativeWord reference in m_traceableHeap.GetObjectPointers(address, typeIndex))
                {
                    int childObjectIndex = m_tracedHeap.ObjectAddressToIndex(reference);
                    if (childObjectIndex != -1)
                    {
                        AddPredecessor(childObjectIndex, parentObjectIndex);
                    }
                }
            }
        }

        void AddPredecessor(int childNodeIndex, int parentNodeIndex)
        {
            if (m_predecessors.TryGetValue(childNodeIndex, out List<int>? nodeIndices))
            {
                nodeIndices!.Add(parentNodeIndex);
            }
            else
            {
                m_predecessors.Add(childNodeIndex, new List<int> { parentNodeIndex });
            }
        }
    }
}
