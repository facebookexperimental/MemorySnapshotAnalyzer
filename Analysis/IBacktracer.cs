// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public interface IBacktracer
    {
        ITracedHeap TracedHeap { get; }

        int RootNodeIndex { get; }

        int NumberOfNodes { get; }

        bool IsLiveObjectNode(int nodeIndex);

        bool IsRootSetNode(int nodeIndex);

        bool IsGCHandle(int nodeIndex);

        int NodeIndexToObjectIndex(int nodeIndex);

        int NodeIndexToRootIndex(int nodeIndex);

        int ObjectIndexToNodeIndex(int objectIndex);

        string DescribeNodeIndex(int nodeIndex, bool fullyQualified);

        string NodeType(int nodeIndex);

        List<int> Predecessors(int nodeIndex);
    }
}
