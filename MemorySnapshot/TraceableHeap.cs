// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class TraceableHeap
    {
        readonly ITypeSystem m_typeSystem;
        readonly Native m_native;
        readonly ulong[] m_gcHandleTargets;

        public TraceableHeap(ITypeSystem typeSystem, Native native, ulong[] gcHandleTargets)
        {
            m_typeSystem = typeSystem;
            m_native = native;
            m_gcHandleTargets = gcHandleTargets;
        }

        public ITypeSystem TypeSystem => m_typeSystem;

        public Native Native => m_native;

        public int NumberOfGCHandles => m_gcHandleTargets.Length;

        public NativeWord GCHandleTarget(int gcHandleIndex)
        {
            return m_native.From(m_gcHandleTargets[gcHandleIndex]);
        }

        public abstract int TryGetTypeIndex(NativeWord objectAddress);

        public abstract int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly);

        public abstract IEnumerable<NativeWord> GetObjectPointers(NativeWord address, int typeIndex);
    }
}
