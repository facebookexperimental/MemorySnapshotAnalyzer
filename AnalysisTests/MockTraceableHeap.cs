/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.AbstractMemorySnapshotTests;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AnalysisTests
{
    class HeapObject
    {
        internal readonly ulong Address;
        internal readonly TestTypeIndex TypeIndex;
        internal readonly Dictionary<int, HeapObject?> Fields;

        internal HeapObject(ulong address, TestTypeIndex typeIndex, Dictionary<int, HeapObject?> fields)
        {
            Address = address;
            TypeIndex = typeIndex;
            Fields = fields;
        }
    }

    sealed class HeapArray : HeapObject
    {
        internal readonly int Length;

        internal HeapArray(ulong address, TestTypeIndex typeIndex, int length, Dictionary<int, HeapObject?> fields)
            : base(address, typeIndex, fields)
        {
            Length = length;
        }
    }

    sealed class MockTraceableHeap : TraceableHeap
    {
        readonly Dictionary<ulong, HeapObject> m_heapObjects;
        readonly List<HeapObject?> m_gcHandles;

        public MockTraceableHeap() : base(new TestTypeSystem())
        {
            m_heapObjects = new();
            HeapObject leafObject = AddHeapObject(0x100, TestTypeIndex.Primitive, new());
            HeapObject innerObject1 = AddHeapObject(0x200, TestTypeIndex.ObjectTwoPointers, new()
            {
                { 16, leafObject },
                { 24, null }, // will be replaced by a pointer to innerObject2
            });
            HeapObject innerObject2 = AddHeapObject(0x300, TestTypeIndex.ObjectTwoPointers, new()
            {
                { 16, innerObject1 },
                { 24, null },
            });
            innerObject1.Fields[24] = innerObject2;

            m_gcHandles = new()
            {
                innerObject2
            };
        }

        #region Test Helpers

        public HeapObject AddHeapObject(ulong address, TestTypeIndex typeIndex, Dictionary<int, HeapObject?> fields)
        {
            var heapObject = new HeapObject(address, typeIndex, fields);
            m_heapObjects.Add(address, heapObject);
            return heapObject;
        }

        public NativeWord GetFieldAtAddress(HeapObject heapObject, int offset)
        {
            return Native.From(heapObject.Fields[offset]?.Address ?? 0);
        }

        #endregion

        public override int NumberOfGCHandles => m_gcHandles.Count;

        public override NativeWord GCHandleTarget(int gcHandleIndex)
        {
            return Native.From(m_gcHandles[gcHandleIndex]?.Address ?? 0);
        }

        public override string Description => throw new System.NotImplementedException();

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            if (m_heapObjects.TryGetValue(objectAddress.Value, out HeapObject? heapObject))
            {
                return (int)heapObject.TypeIndex;
            }
            return -1;
        }

        public override int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly)
        {
            throw new System.NotImplementedException();
        }

        public override string GetObjectNodeType(NativeWord address)
        {
            throw new System.NotImplementedException();
        }

        public override string? GetObjectName(NativeWord objectAddress)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerable<PointerInfo<NativeWord>> GetPointers(NativeWord address, int typeIndex)
        {
            if (TypeSystem.IsArray(typeIndex))
            {
                int elementTypeIndex = TypeSystem.BaseOrElementTypeIndex(typeIndex);
                var heapArray = (HeapArray)m_heapObjects[address.Value];
                for (int i = 0; i < heapArray.Length; i++)
                {
                    foreach (PointerInfo<int> pointerInfo in TypeSystem.GetArrayElementPointerOffsets(elementTypeIndex, TypeSystem.GetArrayElementOffset(elementTypeIndex, i)))
                    {
                        yield return pointerInfo.WithValue(GetFieldAtAddress(heapArray, pointerInfo.Value));
                    }
                }
            }
            else
            {
                var heapObject = m_heapObjects[address.Value];
                foreach (PointerInfo<int> pointerInfo in TypeSystem.GetPointerOffsets(typeIndex, TypeSystem.ObjectHeaderSize(typeIndex)))
                {
                    yield return pointerInfo.WithValue(GetFieldAtAddress(heapObject, pointerInfo.Value));
                }
            }
        }

        public override IEnumerable<(NativeWord childObjectAddress, NativeWord parentObjectAddress)> GetOwningReferencesFromAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerable<(NativeWord objectAddress, List<string> tags)> GetTagsFromAnchor(NativeWord anchorObjectAddress, PointerInfo<NativeWord> pointerInfo)
        {
            throw new System.NotImplementedException();
        }

        public override int NumberOfObjectPairs => throw new System.NotImplementedException();

        public override bool ContainsAddress(NativeWord address)
        {
            throw new System.NotImplementedException();
        }

        public override string? DescribeAddress(NativeWord address)
        {
            throw new System.NotImplementedException();
        }

        public override SegmentedHeap? SegmentedHeapOpt => throw new System.NotImplementedException();
    }
}
