// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityManagedHeap : SegmentedHeap
    {
        readonly UnityManagedTypeSystem m_unityManagedTypeSystem;

        internal UnityManagedHeap(UnityManagedTypeSystem unityTypeSystem, Native native, HeapSegment[] segments, ulong[] gcHandleTargets) :
            base(unityTypeSystem, native, segments, gcHandleTargets)
        {
            m_unityManagedTypeSystem = unityTypeSystem;
        }

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            MemoryView objectView = GetMemoryViewForAddress(objectAddress);
            NativeWord klassPointer = objectView.ReadPointer(0, Native);

            // This is the representation for a heap object when running standalone.
            int typeIndex = m_unityManagedTypeSystem.TypeInfoAddressToIndex(klassPointer);
            if (typeIndex != -1)
            {
                return typeIndex;
            }

            // This is the representation for a heap object when running in the editor.
            MemoryView klassView = GetMemoryViewForAddress(klassPointer);
            if (!klassView.IsValid)
            {
                return -1;
            }
            NativeWord typeInfoAddress = klassView.ReadPointer(0, Native);
            return m_unityManagedTypeSystem.TypeInfoAddressToIndex(typeInfoAddress);
        }

        public override MemoryView StaticFieldBytes(int typeIndex, int fieldNumber)
        {
            return m_unityManagedTypeSystem.StaticFieldBytes(typeIndex, fieldNumber);
        }

        public override int ReadArraySize(MemoryView objectView)
        {
            return m_unityManagedTypeSystem.ReadArraySize(objectView);
        }

        public override int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly)
        {
            MemoryView objectView = GetMemoryViewForAddress(objectAddress);
            return m_unityManagedTypeSystem.GetObjectSize(objectView, typeIndex, committedOnly);
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
    }
}
