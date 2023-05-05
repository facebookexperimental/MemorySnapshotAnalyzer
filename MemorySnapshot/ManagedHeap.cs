// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public sealed class ManagedHeap
    {
        readonly ManagedHeapSegment[] m_segments;

        public ManagedHeap(ManagedHeapSegment[] segments)
        {
            // Unity memory profiler does not dump memory segments sorted by start address.
            Array.Sort(segments, (segment1, segment2) => segment1.StartAddress.Value.CompareTo(segment2.StartAddress.Value));

            // Consistency check that the heap segments are non-overlapping
            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0 && segments[i].StartAddress < segments[i - 1].EndAddress)
                {
                    throw new InvalidSnapshotFormatException("overlapping heap segments");
                }
            }

            m_segments = segments;
        }

        public int NumberOfSegments { get { return m_segments.Length; } }

        public ManagedHeapSegment GetSegment(int index)
        {
            return m_segments[index];
        }

        public ManagedHeapSegment? GetSegmentForAddress(NativeWord address)
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
    }
}
