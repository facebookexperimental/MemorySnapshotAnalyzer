/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using NUnit.Framework;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshotTests
{
    [TestFixture]
    public sealed class SegmentedTraceableHeapTest
    {
        readonly static ulong StartAddress = 0x1000;
        readonly static long HeapSize = 0x1000;

        TestTypeSystem? m_typeSystem;
        Native m_native;
        TestHeapMemory? m_heapMemory;
        TestSegmentedTraceableHeap? m_segmentedTraceableHeap;

        [SetUp]
        public void SetUp()
        {
            m_typeSystem = new();
            m_native = new(m_typeSystem.PointerSize);
            m_heapMemory = new(StartAddress, HeapSize);

            NativeWord startAddress = m_native.From(StartAddress);
            MemoryView memoryView = m_heapMemory.GetRange(0, HeapSize);
            HeapSegment[] segments = new[] { new HeapSegment(startAddress, memoryView, isRuntimeTypeInformation: false) };
            m_segmentedTraceableHeap = new TestSegmentedTraceableHeap(m_typeSystem, segments);
        }

        [Test]
        public void TestBasic()
        {
            // TODO: HeapSegment.ToString()
            // TODO: GetPointers() on object with a pointer
            // TODO: GetPointers() on object with an untraced byte
            // TODO: GetPointers() on object with an untraced short
            // TODO: GetPointers() on object with an untraced int
            // TODO: GetPointers() on object with an untraced long
            // TODO: GetPointers() on array with 2 elements, fully-committed
            // TODO: GetPointers() on array with 2 elements, partially-committed
            Assert.Pass();
        }

        [Test]
        public void TestInterpretSelectors()
        {
            // TODO: test selector that stops in the middle with a null
            // TODO: test selector that stops in the middle with am invalid address
            // TODO: test static selector with [] of references, fully-committed array
            // TODO: test static selector with [] of value types, fully-committed array
            // TODO: test static selector with [] of references, partially-committed array
            // TODO: test static selector with [] of value types, partially-committed array
            // TODO: test selector that stops in the middle of a dynamic tail with am invalid type
            // TODO: test simple dynamic tail, where found type is subtype of static type
            // TODO: test simple dynamic tail, where field is not found
            // TODO: test dynamic tail with [] and that is an array. It has a null element, an element with invalid type, and an element with element of subtype of declared type
            // TODO: test dynamic tail with [] and that is not an array
            // TODO: test dynamic tail that ends in a value type, not an object
            // TODO: test selector starting with static field that is of reference type, null or invalid type
            // TODO: test selector starting with static field that is of value type
            // TODO: test the selector entries_[].value.field
            Assert.Pass();
        }

        [Test]
        public void TestGetFromAnchor()
        {
            // TODO: GetWeightedReferencesFromAnchor()
            // TODO: GetTagsFromAnchor()
        }
    }
}
