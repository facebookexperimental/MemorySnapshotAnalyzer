// Copyright(c) Meta Platforms, Inc. and affiliates.

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
        readonly Dictionary<int, List<int>> m_predecessors;
        readonly HashSet<int> m_ownedNodes;
        readonly HashSet<int> m_nonGCHandleNodes;
        readonly List<(int childNodeIndex, int parentNodeIndex)> m_conditionalOwningReferences;

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
            m_predecessors = new Dictionary<int, List<int>>();
            m_ownedNodes = new HashSet<int>();
            m_nonGCHandleNodes = new HashSet<int>();
            m_conditionalOwningReferences = new List<(int childNodeIndex, int parentNodeIndex)>();
            ComputePredecessors(fuseGCHandles, weakGCHandles);

            // TODO (reference classification): process m_conditionalOwnedReferences
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
                (List<int> rootIndices, _) = m_tracedHeap.PostorderRootIndices(nodeIndex);
                if (rootIndices.Count == 1)
                {
                    return m_rootSet.DescribeRoot(rootIndices[0], fullyQualified);
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
                (List<int> rootIndices, _) = m_tracedHeap.PostorderRootIndices(postorderIndex);
                bool allGCHandles = true;
                foreach (var rootIndex in rootIndices)
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

        public List<int> Predecessors(int nodeIndex)
        {
            return m_predecessors[nodeIndex];
        }

        void ComputePredecessors(bool fuseGCHandles, bool weakGCHandles)
        {
            m_predecessors.Add(m_rootNodeIndex, new List<int>());

            // For each postorder node, add it as a predecessor to all objects it references.
            for (int parentPostorderIndex = 0; parentPostorderIndex < m_tracedHeap.NumberOfPostorderNodes; parentPostorderIndex++)
            {
                NativeWord address = m_tracedHeap.PostorderAddress(parentPostorderIndex);
                int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(parentPostorderIndex);
                if (typeIndex == -1)
                {
                    (List<int> rootIndices, bool isOwningReference) = m_tracedHeap.PostorderRootIndices(parentPostorderIndex);

                    bool isGCHandle = false;
                    if (weakGCHandles)
                    {
                        // Check whether all of the roots are GCHandles. If so, treat this parent as weak.
                        isGCHandle = true;
                        foreach (int rootIndex in rootIndices)
                        {
                            if (!m_rootSet.IsGCHandle(rootIndex))
                            {
                                isGCHandle = false;
                                break;
                            }
                        }
                    }

                    // If this parent index represents a (set of) root nodes, PostOrderAddress above returned the target.
                    int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(address);
                    PointerFlags pointerFlags = isOwningReference ? PointerFlags.IsOwningReference : PointerFlags.None;
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

                    foreach ((NativeWord reference, PointerFlags pointerFlags) in m_traceableHeap.GetIntraHeapPointers(address, typeIndex))
                    {
                        int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(reference);
                        if (childPostorderIndex != -1)
                        {
                            AddPredecessor(childPostorderIndex, resolvedParentPostorderIndex, pointerFlags, isGCHandle: false);
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

            bool isConditionalOwningReference = (pointerFlags & PointerFlags.IsConditionalOwningReference) != 0;
            if (isConditionalOwningReference)
            {
                m_conditionalOwningReferences.Add((childNodeIndex, parentNodeIndex));
            }

            bool isOwningReference = (pointerFlags & PointerFlags.IsOwningReference) != 0;
            if (!isOwningReference && m_ownedNodes.Contains(childNodeIndex))
            {
                return;
            }

            bool isFirstNonGCHandleReferenceToThisChild = !isGCHandle && m_nonGCHandleNodes.Add(childNodeIndex);
            bool isFirstOwningReferenceToThisChild = isOwningReference && m_ownedNodes.Add(childNodeIndex);
            if (m_predecessors.TryGetValue(childNodeIndex, out List<int>? parentNodeIndices))
            {
                if (isFirstNonGCHandleReferenceToThisChild || isFirstOwningReferenceToThisChild)
                {
                    parentNodeIndices!.Clear();
                }

                parentNodeIndices!.Add(parentNodeIndex);
            }
            else
            {
                m_predecessors.Add(childNodeIndex, new List<int> { parentNodeIndex });
            }
        }
    }
}
