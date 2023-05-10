// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityManagedHeap : ManagedHeap
    {
        public UnityManagedHeap(ITypeSystem typeSystem, Native native, ManagedHeapSegment[] segments, ulong[] gcHandleTargets) :
            base(typeSystem, native, segments, gcHandleTargets)
        {
        }

        public override int TryGetTypeIndex(NativeWord address)
        {
            MemoryView objectView = GetMemoryViewForAddress(address);
            if (!objectView.IsValid)
            {
                return -1;
            }
            NativeWord klassPointer = objectView.ReadPointer(TypeSystem.VTableOffsetInHeader, Native);

            // This is the representation for a heap object when running standalone.
            int typeIndex = TypeSystem.TypeInfoAddressToIndex(klassPointer);
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
            return TypeSystem.TypeInfoAddressToIndex(typeInfoAddress);
        }

        // TODO: add method to report cross-heap references
        // (objects of types derived from UnityEngine.Object, with an m_cachedPtr field holding a native object address)
    }
}
