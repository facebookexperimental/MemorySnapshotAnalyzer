// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class UnityManagedHeap : TraceableHeap
    {
        sealed class UnityManagedSegmentedHeap : SegmentedHeap
        {
            readonly UnityManagedTypeSystem m_unityManagedTypeSystem;

            internal UnityManagedSegmentedHeap(UnityManagedTypeSystem unityManagedTypeSystem, HeapSegment[] segments)
                : base(unityManagedTypeSystem, segments)
            {
                // Keep a concretely-typed reference to the type system around.
                m_unityManagedTypeSystem = unityManagedTypeSystem;
            }

            internal UnityManagedTypeSystem UnityManagedTypeSystem => m_unityManagedTypeSystem;

            public override int ReadArraySize(MemoryView objectView)
            {
                return m_unityManagedTypeSystem.ReadArraySize(objectView);
            }
        }

        readonly UnityManagedTypeSystem m_unityManagedTypeSystem;
        readonly UnityManagedSegmentedHeap m_segmentedHeap;
        readonly ulong[] m_gcHandleTargets;

        internal UnityManagedHeap(UnityManagedTypeSystem unityManagedTypeSystem, HeapSegment[] segments, ulong[] gcHandleTargets) :
            base(unityManagedTypeSystem)
        {
            // Keep a concretely-typed reference to the type system around.
            m_unityManagedTypeSystem = unityManagedTypeSystem;
            m_segmentedHeap = new UnityManagedSegmentedHeap(unityManagedTypeSystem, segments);
            m_gcHandleTargets = gcHandleTargets;
        }

        public override int NumberOfGCHandles => m_gcHandleTargets.Length;
    
        public override NativeWord GCHandleTarget(int gcHandleIndex)
        {
            return Native.From(m_gcHandleTargets[gcHandleIndex]);
        }

        // TODO: include user metadata?
        public override string Description => "Unity managed heap";

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            MemoryView objectView = m_segmentedHeap.GetMemoryViewForAddress(objectAddress);
            if (!objectView.IsValid)
            {
                return -1;
            }

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

        public override string GetObjectNodeType(NativeWord address)
        {
            return "object";
        }

        public override string? GetObjectName(NativeWord objectAddress)
        {
            return null;
        }

        public override IEnumerable<NativeWord> GetIntraHeapPointers(NativeWord address, int typeIndex)
        {
            return m_segmentedHeap.GetIntraHeapPointers(address, typeIndex);
        }

        public override IEnumerable<NativeWord> GetInterHeapPointers(NativeWord address, int typeIndex)
        {
            if (m_unityManagedTypeSystem.IsUnityEngineType(typeIndex))
            {
                MemoryView objectView = m_segmentedHeap.GetMemoryViewForAddress(address);
                yield return objectView.ReadPointer(m_unityManagedTypeSystem.UnityEngineCachecPtrFieldOffset, Native);
            }
        }

        public override int NumberOfObjectPairs => 0;

        public override NativeWord GetPrimaryObjectForFusedObject(NativeWord address, NativeWord referrer)
        {
            return address;
        }

        public override bool ContainsAddress(NativeWord address)
        {
            return m_segmentedHeap.GetSegmentForAddress(address) != null;
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

        public override SegmentedHeap? SegmentedHeapOpt => m_segmentedHeap;
    }
}
