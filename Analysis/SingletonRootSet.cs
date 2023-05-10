// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public class SingletonRootSet : IRootSet
    {
        readonly ManagedHeap m_managedHeap;
        readonly NativeWord m_address;

        public SingletonRootSet(ManagedHeap managedHeap, NativeWord address)
        {
            m_managedHeap = managedHeap;
            m_address = address;
        }

        ManagedHeap IRootSet.ManagedHeap => m_managedHeap;

        int IRootSet.NumberOfRoots => 1;

        int IRootSet.NumberOfStaticRoots => 1;

        int IRootSet.NumberOfGCHandles => 0;

        NativeWord IRootSet.GetRoot(int rootIndex)
        {
            return m_address;
        }

        bool IRootSet.IsGCHandle(int rootIndex)
        {
            return false;
        }

        string IRootSet.DescribeRoot(int rootIndex, bool fullyQualified)
        {
            return $"Object@{m_address}";
        }

        string IRootSet.RootType(int rootIndex)
        {
            return "pivot";
        }

        IRootSet.StaticRootInfo IRootSet.GetStaticRootInfo(int rootIndex)
        {
            return default;
        }
    }
}
