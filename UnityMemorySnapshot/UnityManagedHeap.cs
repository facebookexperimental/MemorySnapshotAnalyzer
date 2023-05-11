// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityManagedHeap : ManagedHeap
    {
        readonly UnityTypeSystem m_unityTypeSystem;

        internal UnityManagedHeap(UnityTypeSystem unityTypeSystem, Native native, ManagedHeapSegment[] segments, ulong[] gcHandleTargets) :
            base(unityTypeSystem, native, segments, gcHandleTargets)
        {
            m_unityTypeSystem = unityTypeSystem;
        }

        public override int TryGetTypeIndex(MemoryView objectView)
        {
            NativeWord klassPointer = objectView.ReadPointer(0, Native);

            // This is the representation for a heap object when running standalone.
            int typeIndex = m_unityTypeSystem.TypeInfoAddressToIndex(klassPointer);
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
            return m_unityTypeSystem.TypeInfoAddressToIndex(typeInfoAddress);
        }

        // TODO: add method to report cross-heap references
        // (objects of types derived from UnityEngine.Object, with an m_cachedPtr field holding a native object address)
    }
}
