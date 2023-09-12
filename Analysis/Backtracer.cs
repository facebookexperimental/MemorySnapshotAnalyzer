/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Text;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class Backtracer : IBacktracer
    {
        static readonly string LOG_SOURCE = "Backtracer";

        readonly TracedHeap m_tracedHeap;
        readonly ILogger m_logger;
        readonly IRootSet m_rootSet;
        readonly TraceableHeap m_traceableHeap;
        readonly int m_unreachableNodeIndex;
        readonly int m_rootNodeIndex;
        readonly List<int> m_rootPredecessors;
        readonly List<int>[] m_predecessors;
        readonly Dictionary<int, int> m_nodeWeights;
        readonly Action<string, string> m_logWarning;

        public Backtracer(TracedHeap tracedHeap, ILogger logger, bool fuseRoots)
        {
            m_tracedHeap = tracedHeap;
            m_logger = logger;

            m_rootSet = m_tracedHeap.RootSet;
            m_traceableHeap = m_rootSet.TraceableHeap;

            // For the purposes of backtracing, we assign node indices as follows:
            //   0 ... N-1 : postorder indices (for objects and root sentinels) from TracedHeap
            //   N : unreachable - reserved index for nodes not dominated by the root node
            //   N + 1 : root node - representing the containing process
            m_unreachableNodeIndex = tracedHeap.NumberOfPostorderNodes;
            m_rootNodeIndex = m_unreachableNodeIndex + 1;
            m_rootPredecessors = new List<int>() { m_rootNodeIndex };

            m_predecessors = new List<int>[m_rootNodeIndex + 1];
            m_nodeWeights = new Dictionary<int, int>();

            m_logger.Clear(LOG_SOURCE);
            m_logWarning = LogWarning;

            ComputePredecessors(fuseRoots);

            // Warn about multiple owning references of the same (owning) weight.
            foreach ((int nodeIndex, int weight) in m_nodeWeights)
            {
                if (weight > 0 && m_predecessors[nodeIndex].Count > 1)
                {
                    int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(nodeIndex);
                    string typeName = m_traceableHeap.TypeSystem.QualifiedName(typeIndex);
                    m_logger.Log(LOG_SOURCE, typeName, $"found multiple owning references to object {nodeIndex} (type {typeName}) of weight {weight}");
                }
            }
        }

        public TracedHeap TracedHeap => m_tracedHeap;

        public int RootNodeIndex => m_rootNodeIndex;

        public int UnreachableNodeIndex => m_unreachableNodeIndex;

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
            else if (nodeIndex == m_unreachableNodeIndex)
            {
                return "Unreachable";
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
                $"{m_traceableHeap.TypeSystem.Assembly(typeIndex)}:{m_traceableHeap.TypeSystem.QualifiedName(typeIndex)}" :
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
            else if (nodeIndex == m_unreachableNodeIndex)
            {
                return "unreachable";
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

        public int Weight(int nodeIndex)
        {
            return m_nodeWeights.GetValueOrDefault(nodeIndex, 0);
        }

        public List<int> Predecessors(int nodeIndex)
        {
            return IsRootSentinel(nodeIndex) ? m_rootPredecessors : m_predecessors[nodeIndex];
        }

        void ComputePredecessors(bool fuseRoots)
        {
            m_predecessors[m_rootNodeIndex] = new List<int>();
            m_predecessors[m_unreachableNodeIndex] = new List<int>();

            // For each postorder node, add it as a predecessor to all objects it references.
            for (int parentPostorderIndex = 0; parentPostorderIndex < m_tracedHeap.NumberOfPostorderNodes; parentPostorderIndex++)
            {
                NativeWord address = m_tracedHeap.PostorderAddress(parentPostorderIndex);
                int typeIndex = m_tracedHeap.PostorderTypeIndexOrSentinel(parentPostorderIndex);
                if (typeIndex == -1)
                {
                    List<(int rootIndex, PointerInfo<NativeWord> pointerFlags)> rootInfos = m_tracedHeap.PostorderRootIndices(parentPostorderIndex);

                    bool first = true;
                    int weight = default;
                    foreach ((int rootIndex, PointerInfo<NativeWord> pointerInfo) in rootInfos)
                    {
                        int rootWeight = pointerInfo.PointerFlags.Weight();
                        if (first || rootWeight > weight)
                        {
                            weight = rootWeight;
                        }
                        first = false;

                        bool isWeightAnchor = (pointerInfo.PointerFlags & PointerFlags.IsWeightAnchor) != 0;
                        if (isWeightAnchor)
                        {
                            ProcessWeightAnchor(address, pointerInfo);
                        }
                    }

                    // If this parent index represents a (set of) root nodes, PostOrderAddress above returned the target.
                    int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(address);
                    AddPredecessor(childPostorderIndex, parentPostorderIndex, weight);
                }
                else
                {
                    int resolvedParentPostorderIndex = parentPostorderIndex;
                    if (fuseRoots)
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
                            bool isWeightAnchor = (pointerInfo.PointerFlags & PointerFlags.IsWeightAnchor) != 0;
                            if (isWeightAnchor)
                            {
                                ProcessWeightAnchor(address, pointerInfo);
                            }

                            AddPredecessor(childPostorderIndex, resolvedParentPostorderIndex, pointerInfo.PointerFlags.Weight());
                        }
                    }
                }
            }
        }

        void AddPredecessor(int childNodeIndex, int parentNodeIndex, int weight)
        {
            List<int>? parentNodeIndices = m_predecessors[childNodeIndex];
            if (parentNodeIndices == null)
            {
                parentNodeIndices = new List<int>();
                m_predecessors[childNodeIndex] = parentNodeIndices;
                if (weight != 0)
                {
                    m_nodeWeights[childNodeIndex] = weight;
                }
            }
            else
            {
                int previousWeight = m_nodeWeights.GetValueOrDefault(childNodeIndex, 0);
                if (weight < previousWeight)
                {
                    // Ignore references of a lower weight than previous references to this node.
                    return;
                }
                else if (weight > previousWeight)
                {
                    // If this reference has a higher weight than previous references to this child,
                    // clear previous references.
                    parentNodeIndices.Clear();
                    m_nodeWeights[childNodeIndex] = weight;
                }
            }

            if (!parentNodeIndices.Contains(parentNodeIndex))
            {
                parentNodeIndices.Add(parentNodeIndex);
            }
        }

        void ProcessWeightAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            foreach ((NativeWord childObjectAddress, NativeWord parentObjectAddress, int weight) in m_traceableHeap.GetWeightedReferencesFromAnchor(m_logWarning, anchorObjectAddress, pointerInfo))
            {
                int childPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(childObjectAddress);
                int parentPostorderIndex = m_tracedHeap.ObjectAddressToPostorderIndex(parentObjectAddress);

                if (childPostorderIndex != -1)
                {
                    AddPredecessor(childPostorderIndex, parentPostorderIndex, weight);
                }
            }
        }

        void LogWarning(string location, string message)
        {
            m_logger.Log(LOG_SOURCE, location, message);
        }
    }
}
