/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using NUnit.Framework;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshotTests
{
    sealed class TestSegmentedTraceableHeap : SegmentedTraceableHeap
    {
        sealed class TestSegmentedHeap : SegmentedHeap
        {
            public TestSegmentedHeap(TestSegmentedTraceableHeap traceableHeap, HeapSegment[] segments) : base(traceableHeap, segments)
            {
            }

            public override int ReadArraySize(MemoryView objectView)
            {
                objectView.Read(TestHeapMemory.ArraySizeOffsetInHeader, out int arraySize);
                return arraySize;
            }
        }

        readonly List<ulong> m_gcHandles;

        public TestSegmentedTraceableHeap(TypeSystem typeSystem, HeapSegment[] segments) : base(typeSystem)
        {
            m_gcHandles = new();
            Init(new TestSegmentedHeap(this, segments));
        }

        public override int NumberOfGCHandles => m_gcHandles.Count;

        public override NativeWord GCHandleTarget(int gcHandleIndex)
        {
            return Native.From(m_gcHandles[gcHandleIndex]);
        }

        public override string Description => "TestTraceableHeap";

        int TypeInfoAddressToIndex(NativeWord nativeWord)
        {
            Assert.That(nativeWord.Value, Is.LessThan(TypeSystem.NumberOfTypeIndices));
            return (int)nativeWord.Value;
        }

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            MemoryView objectView = SegmentedHeapOpt!.GetMemoryViewForAddress(objectAddress);
            if (!objectView.IsValid)
            {
                return -1;
            }

            NativeWord klassPointer = objectView.ReadPointer(0, Native);
            return TypeInfoAddressToIndex(klassPointer);
        }

        public override int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly)
        {
            throw new System.NotImplementedException();
        }

        public override string? DescribeAddress(NativeWord address)
        {
            int typeInfoIndex = TypeInfoAddressToIndex(address);
            if (typeInfoIndex != -1)
            {
                return string.Format("VTable[{0}, type index {1}]",
                    TypeSystem.QualifiedName(typeInfoIndex),
                    typeInfoIndex);
            }
            return null;
        }
    }
}
