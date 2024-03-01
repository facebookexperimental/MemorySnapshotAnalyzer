/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public interface IRootSet
    {
        public struct StaticRootInfo
        {
            public string AssemblyName;
            public string NamespaceName;
            public string ClassName;
        };

        TraceableHeap TraceableHeap { get; }

        int NumberOfRoots { get; }

        int NumberOfStaticRoots { get; }

        int NumberOfGCHandles { get; }

        PointerInfo<NativeWord> GetRoot(int rootIndex);

        bool IsGCHandle(int rootIndex);

        string DescribeRoot(int rootIndex, IStructuredOutput output, bool fullyQualified);

        StaticRootInfo GetStaticRootInfo(int rootIndex);
    }
}
