/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class SingletonRootSet : IRootSet
    {
        readonly TraceableHeap m_traceableHeap;
        readonly NativeWord m_address;

        public SingletonRootSet(TraceableHeap traceableHeap, NativeWord address)
        {
            m_traceableHeap = traceableHeap;
            m_address = address;
        }

        TraceableHeap IRootSet.TraceableHeap => m_traceableHeap;

        int IRootSet.NumberOfRoots => 1;

        int IRootSet.NumberOfStaticRoots => 1;

        int IRootSet.NumberOfGCHandles => 0;

        PointerInfo<NativeWord> IRootSet.GetRoot(int rootIndex)
        {
            return new PointerInfo<NativeWord>
            {
                Value = m_address,
                PointerFlags = PointerFlags.None,
                TypeIndex = -1,
                FieldNumber = -1
            };
        }

        bool IRootSet.IsGCHandle(int rootIndex)
        {
            return false;
        }

        string IRootSet.DescribeRoot(int rootIndex, bool fullyQualified)
        {
            return $"Object@{m_address}";
        }

        IRootSet.StaticRootInfo IRootSet.GetStaticRootInfo(int rootIndex)
        {
            return new IRootSet.StaticRootInfo
            {
                AssemblyName = "",
                NamespaceName = "",
                ClassName = ""
            };
        }
    }
}
