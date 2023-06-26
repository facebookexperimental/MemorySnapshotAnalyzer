// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Text;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class Backtracer : IBacktracer
    {
        readonly TracedHeap m_tracedHeap;
        readonly IRootSet m_rootSet;
        readonly TraceableHeap m_traceableHeap;
        readonly int m_rootNodeIndex;
        readonly List<int>[] m_predecessors;
        readonly HashSet<int> m_ownedNodes;
        readonly HashSet<int> m_nonGCHandleNodes;

        public Backtracer(TracedHeap tracedHeap, bool fuseGCHandles, bool weakGCHandles)
        {
            m_tracedHeap = tracedHeap;
            m_rootSet = m_tracedHeap.RootSet;
            m_traceableHeap = m_rootSet.TraceableHeap;

            // For the purposes of backtracing, we assign node indices as follows:
            //   0 ... N-1 : postorder indices (for objects and root sentinels) from TracedHeap
            //   N : root node - representing the containing process
            m_rootNodeIndex = tracedHeap.NumberOfPostorderNodes;

            // TODO: use m_tracedHeap.GetNumberOfPredecessors for a more efficient representation
            m_predecessors = new List<int>[m_rootNodeIndex + 1];
            m_ownedNodes = new HashSet<int>();
            m_nonGCHandleNodes = new HashSet<int>();
            ComputePredecessors(fuseGCHandles, weakGCHandles);
        }

        public TracedHeap TracedHeap => m_tracedHeap;

        public int RootNodeIndex => m_rootNodeIndex;

        public int NumberOfNodes => m_rootNodeIndex + 1;

        public bool IsLiveObjectNode(int nodeIndex)
        {
            return nodeIndex < m_tracedHeap.NumberOfPostorderNodes && !m_tracedHeap.IsRootSentinel(nodeIndex);
        }

        public bool IsRootSentinel(int nodeIndex)
        {
            return nodeIndex < m_tracedHeap.NumberOfPostorderNodes && m_tracedHeap.IsRootSentinel(nodeIndex);
        }

        public int NodeIndexToPostorderIndex(int nodeIndex)
        {
            return nodeIndex < m_tracedHeap.NumberOfPostorderNodes ? nodeIndex : -1;
        }

        public int PostorderIndexToNodeIndex(int postorderIndex)
        {
            return postorderIndex;
        }

        public string DescribeNodeIndex(int nodeIndex, bool fullyQualified)
        {
            if (nodeIndex == m_rootNodeIndex)
            {
                return "Process";
            }

            int postorderIndex = NodeIndexToPostorderIndex(nodeIndex);
            int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
            if (typeIndex == -1)
            {
                List<(int rootIndex, PointerInfo<NativeWord> pointerFlags)> rootInfos = m_tracedHeap.PostorderRootIndices(nodeIndex);
                if (rootInfos.Count == 1)
                {
                    return m_rootSet.DescribeRoot(rootInfos[0].rootIndex, fullyQualified);
                }
                else
                {
                    var sb = new StringBuilder();
                    m_tracedHeap.DescribeRootIndices(nodeIndex, sb);
                    return sb.ToString();
                }
            }

            string typeName = fullyQualified ?
                m_traceableHeap.TypeSystem.QualifiedName(typeIndex) :
                m_traceableHeap.TypeSystem.UnqualifiedName(typeIndex);

            string? objectName = m_traceableHeap.GetObjectName(m_tracedHeap.PostorderAddress(postorderIndex));
            if (objectName != null)
            {
                return string.Format("{0}('{1}')#{2}",
                    typeName,
                    objectName,
                    postorderIndex);
            }
            else
            {
                return string.Format("{0}#{1}",
                    typeName,
                    postorderIndex);
            }
        }

        public string NodeType(int nodeIndex)
        {
            if (nodeIndex == m_rootNodeIndex)
            {
                return "root";
            }

            int postorderIndex = NodeIndexToPostorderIndex(nodeIndex);
            int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(postorderIndex);
            if (typeIndex == -1)
            {
                List<(int rootIndex, PointerInfo<NativeWord> pointerFlags)> rootInfos = m_tracedHeap.PostorderRootIndices(postorderIndex);
                bool allGCHandles = true;
                foreach ((int rootIndex, _) in rootInfos)
                {
                    if (!m_rootSet.IsGCHandle(rootIndex))
                    {
                        allGCHandles = false;
                        break;
                    }
                }

                if (allGCHandles)
                {
                    return "gchandle";
                }
                return "static";
            }
            else if (m_traceableHeap.TypeSystem.IsArray(typeIndex))
            {
                return "array";
            }
            else if (m_traceableHeap.TypeSystem.IsValueType(typeIndex))
            {
                return "box";
            }
            else
            {
                return m_traceableHeap.GetObjectNodeType(m_tracedHeap.PostorderAddress(postorderIndex));
            }
        }

        public bool IsOwned(int nodeIndex)
        {
            return m_ownedNodes.Contains(nodeIndex);
        }

        public List<int> Predecessors(int nodeIndex)
        {
            return m_predecessors[nodeIndex];
        }

        void ComputePredecessors(bool fuseGCHandles, bool weakGCHandles)
        {
            m_predecessors[m_rootNodeIndex] = new List<int>();

            // For each postorder node, add it as a predecessor to all objects it references.
            for (int parentPostorderIndex = 0; parentPostorderIndex < m_tracedHeap.NumberOfPostorderNodes; parentPostorderIndex++)
            {
                NativeWord address = m_tracedHeap.PostorderAddress(parentPostorderIndex);
                int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(parentPostorderIndex);
                if (typeIndex == -1)
                {
                    List<(int rootIndex, PointerInfo<NativeWord> pointerFlags)> rootInfos = m_tracedHeap.PostorderRootIndices(parentPostorderIndex);

                    // Check whether all of the roots are GCHandles. If so, treat this parent as weak.
                    bool isGCHandle = weakGCHandles;
                    PointerFlags pointerFlags = PointerFlags.None;
                    foreach ((int rootIndex, PointerInfo<NativeWord> pointerInfo) in rootInfos)
                    {
                        if (!m_rootSet.IsGCHandle(rootIndex))
                        {
                            if (!m_rootSet.IsGCHandle(rootIndex))
                            {
                                isGCHandle = false;
                            }
                        }

                        pointerFlags |= pointerInfo.PointerFlags;

                        bool isConditionAnchor = (pointerInfo.PointerFlags & PointerFlags.IsConditionAnchor) != 0;
                        if (isConditionAnchor)
                        {
                            ProcessConditionAnchor(address, pointerInfo);
                        }
                    }

                    // If this parent index represents a (set of) root nodes, PostOrderAddress above returned the target.
                    int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(address);
                    AddPredecessor(childPostorderIndex, parentPostorderIndex, pointerFlags, isGCHandle: isGCHandle);
                }
                else
                {
                    int resolvedParentPostorderIndex = parentPostorderIndex;
                    if (fuseGCHandles)
                    {
                        // Redirect parentPostorderIndex to root postorder index, if available.
                        int rootPostorderIndex = m_tracedHeap.ObjectAddressToRootPostorderIndex(address);
                        if (rootPostorderIndex != -1)
                        {
                            resolvedParentPostorderIndex = rootPostorderIndex;
                        }
                    }

                    foreach (PointerInfo<NativeWord> pointerInfo in m_traceableHeap.GetIntraHeapPointers(address, typeIndex))
                    {
                        int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(pointerInfo.Value);
                        if (childPostorderIndex != -1)
                        {
                            bool isConditionAnchor = (pointerInfo.PointerFlags & PointerFlags.IsConditionAnchor) != 0;
                            if (isConditionAnchor)
                            {
                                ProcessConditionAnchor(address, pointerInfo);
                            }

                            AddPredecessor(childPostorderIndex, resolvedParentPostorderIndex, pointerInfo.PointerFlags, isGCHandle: false);
                        }
                    }
                }
            }
        }

        void AddPredecessor(int childNodeIndex, int parentNodeIndex, PointerFlags pointerFlags, bool isGCHandle)
        {
            if (isGCHandle && m_nonGCHandleNodes.Contains(childNodeIndex))
            {
                return;
            }

            bool isOwningReference = (pointerFlags & PointerFlags.IsOwningReference) != 0;
            if (!isOwningReference && m_ownedNodes.Contains(childNodeIndex))
            {
                return;
            }

            bool isFirstNonGCHandleReferenceToThisChild = !isGCHandle && m_nonGCHandleNodes.Add(childNodeIndex);
            bool isFirstOwningReferenceToThisChild = isOwningReference && m_ownedNodes.Add(childNodeIndex);
            List<int>? parentNodeIndices = m_predecessors[childNodeIndex];
            if (parentNodeIndices == null)
            {
                parentNodeIndices = new List<int>();
                m_predecessors[childNodeIndex] = parentNodeIndices;
            }
            else if (isFirstNonGCHandleReferenceToThisChild || isFirstOwningReferenceToThisChild)
            {
                parentNodeIndices.Clear();
            }
            else
            {
                if (parentNodeIndices.Contains(parentNodeIndex))
                {
                    return;
                }

                if (isOwningReference && !isFirstOwningReferenceToThisChild)
                {
                    // TODO: better warning management
                    int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(childNodeIndex);
                    string typeName = m_traceableHeap.TypeSystem.QualifiedName(typeIndex);
                    Console.Error.WriteLine($"found multiple owning references to object {childNodeIndex} of type {typeName}");
                }
            }

            parentNodeIndices.Add(parentNodeIndex);
        }

        void ProcessConditionAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress) in m_traceableHeap.GetOwningReferencesFromAnchor(anchorObjectAddress, pointerInfo))
            {
                int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(childObjectAddress);
                int parentPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(parentObjectAddress);

                if (childPostorderIndex != -1)
                {
                    AddPredecessor(childPostorderIndex, parentPostorderIndex, PointerFlags.IsOwningReference, isGCHandle: false);
                }
            }
        }
    }
}
