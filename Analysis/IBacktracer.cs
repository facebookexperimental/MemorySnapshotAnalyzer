// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public interface IBacktracer
    {
        TracedHeap TracedHeap { get; }

        int RootNodeIndex { get; }

        int NumberOfNodes { get; }

        bool IsLiveObjectNode(int nodeIndex);

        bool IsRootSentinel(int nodeIndex);

        int NodeIndexToPostorderIndex(int nodeIndex);

        int PostorderIndexToNodeIndex(int postorderIndex);

        string DescribeNodeIndex(int nodeIndex, bool fullyQualified);

        string NodeType(int nodeIndex);

        List<int> Predecessors(int nodeIndex);
    }
}
