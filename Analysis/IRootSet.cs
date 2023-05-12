// Copyright(c) Meta Platforms, Inc. and affiliates.

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

        NativeWord GetRoot(int rootIndex);

        bool IsGCHandle(int rootIndex);

        string DescribeRoot(int rootIndex, bool fullyQualified);

        string RootType(int rootIndex);

        StaticRootInfo GetStaticRootInfo(int rootIndex);
    }
}
