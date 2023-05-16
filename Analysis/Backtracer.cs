// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class Backtracer : IBacktracer
    {
        readonly TracedHeap m_tracedHeap;
        readonly IRootSet m_rootSet;
        readonly TraceableHeap m_traceableHeap;
        readonly int m_rootNodeIndex;
        readonly Dictionary<int, List<int>> m_predecessors;

        public Backtracer(TracedHeap tracedHeap, bool fuseGCHandles)
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
            ComputePredecessors(fuseGCHandles);
        }

        public TracedHeap TracedHeap => m_tracedHeap;

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
                string typeName = fullyQualified ?
                    m_traceableHeap.TypeSystem.QualifiedName(typeIndex) :
                    m_traceableHeap.TypeSystem.UnqualifiedName(typeIndex);

                string? objectName = m_traceableHeap.GetObjectName(m_tracedHeap.ObjectAddress(objectIndex));
                if (objectName != null)
                {
                    return string.Format("{0}('{1}')#{2}",
                        typeName,
                        objectName,
                        objectIndex);
                }
                else
                {
                    return string.Format("{0}#{1}",
                        typeName,
                        objectIndex);
                }
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
                    return m_traceableHeap.GetObjectNodeType(m_tracedHeap.ObjectAddress(objectIndex));
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

        void ComputePredecessors(bool fuseGCHandles)
        {
            m_predecessors.Add(m_rootNodeIndex, new List<int>());

            // If fusing GCHandles with their target objects, construct the helper data structure for fusing them.
            Dictionary<ulong, int>? fusedGCHandles;
            if (fuseGCHandles)
            {
                fusedGCHandles = new Dictionary<ulong, int>();
                for (int rootIndex = 0; rootIndex < m_rootSet.NumberOfRoots; rootIndex++)
                {
                    NativeWord reference = m_rootSet.GetRoot(rootIndex);
                    int objectIndex = m_tracedHeap.ObjectAddressToIndex(reference);
                    if (objectIndex != -1)
                    {
                        fusedGCHandles[reference.Value] = m_tracedHeap.NumberOfLiveObjects + rootIndex;
                    }
                }
            }
            else
            {
                fusedGCHandles = null;
            }

            // Each GCHandle is a predecessor for its target.
            for (int rootIndex = 0; rootIndex < m_rootSet.NumberOfRoots; rootIndex++)
            {
                NativeWord reference = m_rootSet.GetRoot(rootIndex);
                int objectIndex = m_tracedHeap.ObjectAddressToIndex(reference);
                if (objectIndex != -1)
                {
                    AddPredecessor(objectIndex, m_tracedHeap.NumberOfLiveObjects + rootIndex);
                }
            }

            // For each object node, add it as a predecessor to all objects it has references to.
            for (int parentObjectIndex = 0; parentObjectIndex < m_tracedHeap.NumberOfLiveObjects; parentObjectIndex++)
            {
                NativeWord address = m_tracedHeap.ObjectAddress(parentObjectIndex);
                int typeIndex = m_tracedHeap.ObjectTypeIndex(parentObjectIndex);
                foreach (NativeWord reference in m_traceableHeap.GetIntraHeapPointers(address, typeIndex))
                {
                    int childObjectIndex = FuseGCHandleAndObject(reference, fusedGCHandles);
                    if (childObjectIndex != -1)
                    {
                        AddPredecessor(childObjectIndex, parentObjectIndex);
                    }
                }
            }

            // Parent any GCHandles that don't yet have any predecessor to the global root node.
            for (int rootIndex = 0; rootIndex < m_rootSet.NumberOfRoots; rootIndex++)
            {
                if (!m_predecessors.ContainsKey(m_tracedHeap.NumberOfLiveObjects + rootIndex))
                {
                    AddPredecessor(m_tracedHeap.NumberOfLiveObjects + rootIndex, m_rootNodeIndex);
                }
            }
        }

        int FuseGCHandleAndObject(NativeWord reference, Dictionary<ulong, int>? fusedGCHandles)
        {
            // TODO: this does not preserve reverse postorder
            if (fusedGCHandles != null && fusedGCHandles.TryGetValue(reference.Value, out int nodeIndex))
            {
                return nodeIndex;
            }
            return m_tracedHeap.ObjectAddressToIndex(reference);
        }

        void AddPredecessor(int childNodeIndex, int parentNodeIndex)
        {
            if (m_predecessors.TryGetValue(childNodeIndex, out List<int>? parentNodeIndices))
            {
                parentNodeIndices!.Add(parentNodeIndex);
            }
            else
            {
                m_predecessors.Add(childNodeIndex, new List<int> { parentNodeIndex });
            }
        }
    }
}
