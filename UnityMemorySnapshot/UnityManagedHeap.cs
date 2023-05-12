// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityManagedHeap : TraceableHeap
    {
        sealed class UnityManagedSegmentedHeap : SegmentedHeap
        {
            readonly UnityManagedTypeSystem m_unityManagedTypeSystem;

            internal UnityManagedSegmentedHeap(UnityManagedTypeSystem unityManagedTypeSystem, Native native, HeapSegment[] segments)
                : base(unityManagedTypeSystem, native, segments)
            {
                // Keep a concretely-typed reference to the type system around.
                m_unityManagedTypeSystem = unityManagedTypeSystem;
            }

            internal UnityManagedTypeSystem UnityManagedTypeSystem => m_unityManagedTypeSystem;

            public override MemoryView StaticFieldBytes(int typeIndex, int fieldNumber)
            {
                return m_unityManagedTypeSystem.StaticFieldBytes(typeIndex, fieldNumber);
            }

            public override int ReadArraySize(MemoryView objectView)
            {
                return m_unityManagedTypeSystem.ReadArraySize(objectView);
            }
        }

        readonly UnityManagedTypeSystem m_unityManagedTypeSystem;
        readonly UnityManagedSegmentedHeap m_segmentedHeap;

        internal UnityManagedHeap(UnityManagedTypeSystem unityManagedTypeSystem, Native native, HeapSegment[] segments, ulong[] gcHandleTargets) :
            base(unityManagedTypeSystem, native, gcHandleTargets)
        {
            // Keep a concretely-typed reference to the type system around.
            m_unityManagedTypeSystem = unityManagedTypeSystem;

            m_segmentedHeap = new UnityManagedSegmentedHeap(unityManagedTypeSystem, native, segments);
        }

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            MemoryView objectView = m_segmentedHeap.GetMemoryViewForAddress(objectAddress);
            NativeWord klassPointer = objectView.ReadPointer(0, Native);

            // This is the representation for a heap object when running standalone.
            int typeIndex = m_segmentedHeap.UnityManagedTypeSystem.TypeInfoAddressToIndex(klassPointer);
            if (typeIndex != -1)
            {
                return typeIndex;
            }

            // This is the representation for a heap object when running in the editor.
            MemoryView klassView = m_segmentedHeap.GetMemoryViewForAddress(klassPointer);
            if (!klassView.IsValid)
            {
                return -1;
            }
            NativeWord typeInfoAddress = klassView.ReadPointer(0, Native);
            return m_segmentedHeap.UnityManagedTypeSystem.TypeInfoAddressToIndex(typeInfoAddress);
        }

        public override int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly)
        {
            MemoryView objectView = m_segmentedHeap.GetMemoryViewForAddress(objectAddress);
            return m_segmentedHeap.UnityManagedTypeSystem.GetObjectSize(objectView, typeIndex, committedOnly);
        }

        public override string? GetObjectName(NativeWord objectAddress)
        {
            return null;
        }

        public override IEnumerable<NativeWord> GetObjectPointers(NativeWord address, int typeIndex)
        {
            return m_segmentedHeap.GetObjectPointers(address, typeIndex);
        }

        public override string? DescribeAddress(NativeWord address)
        {
            int typeInfoIndex = m_unityManagedTypeSystem.TypeInfoAddressToIndex(address);
            if (typeInfoIndex != -1)
            {
                return string.Format("VTable[{0}, type index {1}]",
                    m_unityManagedTypeSystem.QualifiedName(typeInfoIndex),
                    typeInfoIndex);
            }
            return null;
        }

        // TODO: add method to report cross-heap references
        // (objects of types derived from UnityEngine.Object, with an m_cachedPtr field holding a native object address)

        public override SegmentedHeap? SegmentedHeapOpt => m_segmentedHeap;
    }
}
