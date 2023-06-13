﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class StitchedTraceableHeap : TraceableHeap
    {
        readonly TraceableHeap m_primary;
        readonly TraceableHeap m_secondary;
        readonly string m_description;
        readonly bool m_computingObjectPairs;
        readonly Dictionary<ulong, ulong>? m_fusedObjectParent;

        public StitchedTraceableHeap(TraceableHeap primary, TraceableHeap secondary, bool fuseObjectPairs) :
            base(new StitchedTypeSystem(primary.TypeSystem, secondary.TypeSystem))
        {
            m_primary = primary;
            m_secondary = secondary;
            m_description = $"{primary.Description} -> {secondary.Description}";

            if (fuseObjectPairs)
            {
                m_computingObjectPairs = true;
                m_fusedObjectParent = new Dictionary<ulong, ulong>();
                // Perform heap tracing once, for the side effect of discovering all object pairs.
                var _ = new TracedHeap(new RootSet(this), weakGCHandles: false);
                m_computingObjectPairs = false;
            }
        }

        public override string Description => m_description;

        public override int NumberOfGCHandles => m_primary.NumberOfGCHandles;

        public override NativeWord GCHandleTarget(int gcHandleIndex)
        {
            // We are only considering the primary heap to have GCHandles.
            // Everything on the secondary heap is reachable only through cross-heap pointers from the primary heap.
            return m_primary.GCHandleTarget(gcHandleIndex);
        }

        public override int TryGetTypeIndex(NativeWord objectAddress)
        {
            int typeIndex = m_secondary.TryGetTypeIndex(objectAddress);
            if (typeIndex != -1)
            {
                return m_primary.TypeSystem.NumberOfTypeIndices + typeIndex;
            }

            return m_primary.TryGetTypeIndex(objectAddress);
        }

        public override int GetObjectSize(NativeWord objectAddress, int typeIndex, bool committedOnly)
        {
            if (m_secondary.ContainsAddress(objectAddress))
            {
                return m_secondary.GetObjectSize(objectAddress, typeIndex - m_primary.TypeSystem.NumberOfTypeIndices, committedOnly);
            }
            return m_primary.GetObjectSize(objectAddress, typeIndex, committedOnly);
        }

        public override string GetObjectNodeType(NativeWord objectAddress)
        {
            return m_secondary.ContainsAddress(objectAddress) ? m_secondary.GetObjectNodeType(objectAddress) : m_primary.GetObjectNodeType(objectAddress);
        }

        public override string? GetObjectName(NativeWord objectAddress)
        {
            return m_secondary.ContainsAddress(objectAddress) ? m_secondary.GetObjectName(objectAddress) : m_primary.GetObjectName(objectAddress);
        }

        public override IEnumerable<NativeWord> GetIntraHeapPointers(NativeWord address, int typeIndex)
        {
            if (m_secondary.ContainsAddress(address))
            {
                foreach (NativeWord reference in m_secondary.GetIntraHeapPointers(address, typeIndex - m_primary.TypeSystem.NumberOfTypeIndices))
                {
                    if (m_fusedObjectParent != null && !m_computingObjectPairs)
                    {
                        if (m_fusedObjectParent.TryGetValue(reference.Value, out ulong parentAddress))
                        {
                            yield return Native.From(parentAddress);
                            continue;
                        }
                    }

                    yield return reference;
                }
            }
            else
            {
                foreach (NativeWord reference in m_primary.GetIntraHeapPointers(address, typeIndex))
                {
                    yield return reference;
                }

                foreach (NativeWord reference in m_primary.GetInterHeapPointers(address, typeIndex))
                {
                    if (m_computingObjectPairs)
                    {
                        // We assume that managed and native objects reference one another 1:1.
                        // We use "TryAdd" so that the data structure doesn't keep changing
                        // if future calls discover that the assumption is not true.
                        m_fusedObjectParent!.TryAdd(reference.Value, address.Value);
                    }

                    yield return reference;
                }
            }
        }

        public override IEnumerable<NativeWord> GetInterHeapPointers(NativeWord address, int typeIndex)
        {
            return Array.Empty<NativeWord>();
        }

        public override int NumberOfObjectPairs => m_fusedObjectParent == null ? 0 : m_fusedObjectParent.Count;

        public override bool ContainsAddress(NativeWord address)
        {
            return m_secondary.ContainsAddress(address) || m_primary.ContainsAddress(address);
        }

        public override string? DescribeAddress(NativeWord address)
        {
            string? secondaryDescription = m_secondary.DescribeAddress(address);
            string? primaryDescription = m_primary.DescribeAddress(address);
            if (secondaryDescription != null && primaryDescription != null)
            {
                return $"{primaryDescription}/{secondaryDescription}";
            }
            return primaryDescription ?? secondaryDescription;
        }

        // TODO: if both heaps have a segmented heap, we can try to stitch those together as well
        public override SegmentedHeap? SegmentedHeapOpt => null;
    }
}