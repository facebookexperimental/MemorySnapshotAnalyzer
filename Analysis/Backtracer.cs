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
        readonly HashSet<int> m_strongNodes;

        public enum Options
        {
            None = 0,
            FuseRoots = 1 << 0,
            WeakGCHandles = 1 << 1,
        }

        public Backtracer(TracedHeap tracedHeap, Options options)
        {
            m_tracedHeap = tracedHeap;
            m_rootSet = m_tracedHeap.RootSet;
            m_traceableHeap = m_rootSet.TraceableHeap;

            // For the purposes of backtracing, we assign node indices as follows:
            //   0 ... N-1 : postorder indices (for objects and root sentinels) from TracedHeap
            //   N : root node - representing the containing process
            m_rootNodeIndex = tracedHeap.NumberOfPostorderNodes;

            m_predecessors = new List<int>[m_rootNodeIndex + 1];
            m_ownedNodes = new HashSet<int>();
            m_strongNodes = new HashSet<int>();

            ComputePredecessors(options);
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

        public bool IsWeak(int nodeIndex)
        {
            return !m_strongNodes.Contains(nodeIndex);
        }

        public List<int> Predecessors(int nodeIndex)
        {
            return m_predecessors[nodeIndex];
        }

        void ComputePredecessors(Options options)
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
                    bool isGCHandle = (options & Options.WeakGCHandles) != 0;
                    PointerFlags pointerFlags = PointerFlags.None;
                    foreach ((int rootIndex, PointerInfo<NativeWord> pointerInfo) in rootInfos)
                    {
                        if (!m_rootSet.IsGCHandle(rootIndex))
                        {
                            isGCHandle = false;
                        }

                        pointerFlags |= pointerInfo.PointerFlags;

                        bool isConditionAnchor = (pointerInfo.PointerFlags & PointerFlags.IsConditionAnchor) != 0;
                        if (isConditionAnchor)
                        {
                            ProcessConditionAnchor(address, pointerInfo);
                        }
                    }

                    if (isGCHandle)
                    {
                        pointerFlags |= PointerFlags.IsWeakReference;
                    }

                    // If this parent index represents a (set of) root nodes, PostOrderAddress above returned the target.
                    int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(address);
                    AddPredecessor(childPostorderIndex, parentPostorderIndex, pointerFlags);
                }
                else
                {
                    int resolvedParentPostorderIndex = parentPostorderIndex;
                    if ((options & Options.FuseRoots) != 0)
                    {
                        // Redirect parentPostorderIndex to root postorder index, if available.
                        int rootPostorderIndex = m_tracedHeap.ObjectAddressToRootPostorderIndex(address);
                        if (rootPostorderIndex != -1)
                        {
                            resolvedParentPostorderIndex = rootPostorderIndex;
                        }
                    }

                    foreach (PointerInfo<NativeWord> pointerInfo in m_traceableHeap.GetPointers(address, typeIndex))
                    {
                        int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(pointerInfo.Value);
                        if (childPostorderIndex != -1)
                        {
                            bool isConditionAnchor = (pointerInfo.PointerFlags & PointerFlags.IsConditionAnchor) != 0;
                            if (isConditionAnchor)
                            {
                                ProcessConditionAnchor(address, pointerInfo);
                            }

                            AddPredecessor(childPostorderIndex, resolvedParentPostorderIndex, pointerInfo.PointerFlags);
                        }
                    }
                }
            }
        }

        void AddPredecessor(int childNodeIndex, int parentNodeIndex, PointerFlags pointerFlags)
        {
            bool isOwningReference = (pointerFlags & PointerFlags.IsOwningReference) != 0;
            bool isStrongReference = (pointerFlags & PointerFlags.IsWeakReference) == 0;
            bool clearPreviousReferences;

            bool warnAboutMultipleOwningReferences = false;
            if (isOwningReference)
            {
                // If this is the first owning reference to this child, clear previous references
                // (which must have been non-owning, either strong or weak). Also, this marks the node
                // as being owned.
                clearPreviousReferences = m_ownedNodes.Add(childNodeIndex);

                // If this is not the first owning reference to this child, report a warning that
                // there there is more than one owning reference to this child.
                if (!clearPreviousReferences)
                {
                    warnAboutMultipleOwningReferences = true;
                }
            }
            else if (m_ownedNodes.Contains(childNodeIndex))
            {
                // Ignore non-owning references to nodes for which we already found owning references.
                return;
            }
            else if (isStrongReference)
            {
                // If this is the first strong reference to this node (and we have not found owning references),
                // clear previous references (which must all have been weak). Also, this marks the node
                // as being strongly referenced.
                clearPreviousReferences = m_strongNodes.Add(childNodeIndex);
            }
            else if (m_strongNodes.Contains(childNodeIndex))
            {
                // Ignore weak, non-owning references to nodes for which we already found strong references.
                return;
            }
            else
            {
                clearPreviousReferences = false;
            }

            List<int>? parentNodeIndices = m_predecessors[childNodeIndex];
            if (parentNodeIndices == null)
            {
                parentNodeIndices = new List<int>();
                m_predecessors[childNodeIndex] = parentNodeIndices;
            }
            else if (clearPreviousReferences)
            {
                parentNodeIndices.Clear();
            }
            else if (parentNodeIndices.Contains(parentNodeIndex))
            {
                return;
            }
            else if (warnAboutMultipleOwningReferences)
            {
                // TODO: better warning management
                int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(childNodeIndex);
                string typeName = m_traceableHeap.TypeSystem.QualifiedName(typeIndex);
                Console.Error.WriteLine($"found multiple owning references to object {childNodeIndex} of type {typeName}");
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
                    AddPredecessor(childPostorderIndex, parentPostorderIndex, PointerFlags.IsOwningReference);
                }
            }
        }
    }
}
