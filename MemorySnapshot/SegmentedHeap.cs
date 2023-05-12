// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class SegmentedHeap
    {
        readonly ITypeSystem m_typeSystem;
        readonly Native m_native;
        readonly HeapSegment[] m_segments;

        public SegmentedHeap(ITypeSystem typeSystem, Native native, HeapSegment[] segments)
        {
            m_typeSystem = typeSystem;
            m_native = native;

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

        public IEnumerable<NativeWord> GetObjectPointers(NativeWord address, int typeIndex)
        {
            MemoryView objectView = GetMemoryViewForAddress(address);
            if (m_typeSystem.IsArray(typeIndex))
            {
                int elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
                int arraySize = ReadArraySize(objectView);
                for (int i = 0; i < arraySize; i++)
                {
                    foreach (int offset in GetFieldPointerOffsets(elementTypeIndex, m_typeSystem.GetArrayElementOffset(elementTypeIndex, i)))
                    {
                        // We can find arrays whose backing store has not been fully committed.
                        if (offset + m_typeSystem.PointerSize > objectView.Size)
                        {
                            break;
                        }

                        yield return objectView.ReadPointer(offset, m_native);
                    }
                }
            }
            else
            {
                foreach (int offset in GetPointerOffsets(typeIndex, baseOffset: 0, hasHeader: true))
                {
                    yield return objectView.ReadPointer(offset, m_native);
                }
            }
        }

        public IEnumerable<int> GetFieldPointerOffsets(int typeIndex, int baseOffset)
        {
            if (m_typeSystem.IsValueType(typeIndex))
            {
                foreach (int offset in GetPointerOffsets(typeIndex, baseOffset, hasHeader: false))
                {
                    yield return offset;
                }
            }
            else
            {
                yield return baseOffset;
            }
        }

        IEnumerable<int> GetPointerOffsets(int typeIndex, int baseOffset, bool hasHeader)
        {
            int numberOfFields = m_typeSystem.NumberOfFields(typeIndex);
            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                if (!m_typeSystem.FieldIsStatic(typeIndex, fieldNumber))
                {
                    int fieldTypeIndex = m_typeSystem.FieldType(typeIndex, fieldNumber);
                    int fieldOffset = m_typeSystem.FieldOffset(typeIndex, fieldNumber, withHeader: hasHeader);
                    if (!hasHeader && fieldTypeIndex == typeIndex)
                    {
                        // Avoid infinite recursion due to the way that primitive types (such as System.Int32) are defined.
                        continue;
                    }

                    foreach (int offset in GetFieldPointerOffsets(fieldTypeIndex, baseOffset + fieldOffset))
                    {
                        yield return offset;
                    }
                }
            }

            int baseOrElementTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
            if (baseOrElementTypeIndex >= 0)
            {
                foreach (int offset in GetPointerOffsets(baseOrElementTypeIndex, baseOffset, hasHeader))
                {
                    yield return offset;
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

        public abstract MemoryView StaticFieldBytes(int typeIndex, int fieldNumber);

        public abstract int ReadArraySize(MemoryView objectView);
    }
}
