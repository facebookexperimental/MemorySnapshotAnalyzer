/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public interface IBacktracer
    {
        TracedHeap TracedHeap { get; }

        int RootNodeIndex { get; }

        int UnreachableNodeIndex { get; }

        int NumberOfNodes { get; }

        bool IsLiveObjectNode(int nodeIndex);

        bool IsRootSentinel(int nodeIndex);

        int NodeIndexToPostorderIndex(int nodeIndex);

        int PostorderIndexToNodeIndex(int postorderIndex);

        string DescribeNodeIndex(int nodeIndex, IStructuredOutput output, bool fullyQualified);

        string NodeType(int nodeIndex);

        int Weight(int nodeIndex);

        List<int> Predecessors(int nodeIndex);
    }
}
