﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class TraceableHeap
    {
        readonly ITypeSystem m_typeSystem;
        readonly Native m_native;

        public TraceableHeap(ITypeSystem typeSystem)
        {
            m_typeSystem = typeSystem;
            m_native = new Native(typeSystem.PointerSize);
        }

        public ITypeSystem TypeSystem => m_typeSystem;

        public Native Native => m_native;

        public abstract int NumberOfGCHandles { get; }

        public abstract NativeWord GCHandleTarget(int gcHandleIndex);

        public abstract string Description { get; }

        public abstract int TryGetTypeIndex(NativeWord objectAddress);

        public abstract int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly);

        public abstract string GetObjectNodeType(NativeWord address);

        public abstract string? GetObjectName(NativeWord objectAddress);

        public abstract IEnumerable<NativeWord> GetObjectPointers(NativeWord address, int typeIndex, bool includeCrossHeapReferences);

        public abstract bool ContainsAddress(NativeWord address);

        public abstract string? DescribeAddress(NativeWord address);

        public abstract SegmentedHeap? SegmentedHeapOpt { get; }
    }
}
