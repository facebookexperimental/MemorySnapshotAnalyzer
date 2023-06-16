// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using static MemorySnapshotAnalyzer.AbstractMemorySnapshot.TraceableHeap;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class SegmentedHeap
    {
        readonly TypeSystem m_typeSystem;
        readonly Native m_native;
        readonly HeapSegment[] m_segments;

        public SegmentedHeap(TypeSystem typeSystem, HeapSegment[] segments)
        {
            m_typeSystem = typeSystem;
            m_native = new Native(typeSystem.PointerSize);

            // Consistency check that the heap segments are ordered and non-overlapping
            for (int i = 1; i < segments.Length; i++)
            {
                if (segments[i].StartAddress < segments[i - 1].EndAddress)
                {
                    throw new InvalidSnapshotFormatException("unordered or overlapping heap segments");
                }
            }

            m_segments = segments;
        }

        // This method provides an implementation for TraceableHeap.GetIntraHeapPointers, for heaps whose memory we have access to.
        public IEnumerable<(NativeWord reference, PointerFlags pointerFlags)> GetIntraHeapPointers(NativeWord address, int typeIndex)
        {
            MemoryView objectView = GetMemoryViewForAddress(address);
            if (m_typeSystem.IsArray(typeIndex))
            {
                int elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
                int arraySize = ReadArraySize(objectView);
                for (int i = 0; i < arraySize; i++)
                {
                    foreach ((int offset, PointerFlags pointerFlags) in m_typeSystem.GetArrayElementPointerOffsets(elementTypeIndex, m_typeSystem.GetArrayElementOffset(elementTypeIndex, i)))
                    {
                        // We can find arrays whose backing store has not been fully committed.
                        if (offset + m_typeSystem.PointerSize > objectView.Size)
                        {
                            break;
                        }

                        yield return (objectView.ReadPointer(offset, m_native), pointerFlags);
                    }
                }
            }
            else
            {
                foreach ((int offset, PointerFlags pointerFlags) in m_typeSystem.GetPointerOffsets(typeIndex, m_typeSystem.ObjectHeaderSize(typeIndex)))
                {
                    yield return (objectView.ReadPointer(offset, m_native), pointerFlags);
                }
            }
        }

        public int NumberOfSegments { get { return m_segments.Length; } }

        public HeapSegment GetSegment(int index)
        {
            return m_segments[index];
        }

        public HeapSegment? GetSegmentForAddress(NativeWord address)
        {
            int min = 0;
            int max = m_segments.Length;
            while (min < max)
            {
                int mid = (min + max) / 2;
                if (m_segments[mid].StartAddress <= address && address < m_segments[mid].EndAddress)
                {
                    return m_segments[mid];
                }
                else if (m_segments[mid].StartAddress < address)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid;
                }
            }

            return null;
        }

        public MemoryView GetMemoryViewForAddress(NativeWord address)
        {
            HeapSegment? segment = GetSegmentForAddress(address);
            if (segment == null)
            {
                return default;
            }

            long offset = (long)(address - segment.StartAddress).Value;
            return segment.MemoryView.GetRange(offset, segment.Size - offset);
        }

        public abstract int ReadArraySize(MemoryView objectView);
    }
}
