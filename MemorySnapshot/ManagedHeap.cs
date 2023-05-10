// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class ManagedHeap
    {
        readonly ITypeSystem m_typeSystem;
        readonly Native m_native;
        readonly ManagedHeapSegment[] m_segments;
        readonly ulong[] m_gcHandleTargets;

        public ManagedHeap(ITypeSystem typeSystem, Native native, ManagedHeapSegment[] segments, ulong[] gcHandleTargets)
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
            m_gcHandleTargets = gcHandleTargets;
        }

        public ITypeSystem TypeSystem => m_typeSystem;

        public Native Native => m_native;

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

        public int NumberOfGCHandles => m_gcHandleTargets.Length;

        public NativeWord GCHandleTarget(int gcHandleIndex)
        {
            return m_native.From(m_gcHandleTargets[gcHandleIndex]);
        }

        public MemoryView GetMemoryViewForAddress(NativeWord address)
        {
            ManagedHeapSegment? segment = GetSegmentForAddress(address);
            if (segment == null)
            {
                return default;
            }

            long offset = (long)(address - segment.StartAddress).Value;
            return segment.MemoryView.GetRange(offset, segment.Size - offset);
        }

        public abstract int TryGetTypeIndex(MemoryView objectView);

        public int GetObjectSize(MemoryView objectView, int typeIndex, bool committedOnly)
        {
            if (TypeSystem.IsArray(typeIndex))
            {
                objectView.Read(TypeSystem.ArraySizeOffsetInHeader, out int arraySize);
                int elementSize = GetElementSize(TypeSystem.BaseOrElementTypeIndex(typeIndex));
                int arraySizeInBytes = RoundToAllocationGranularity(TypeSystem.ArrayFirstElementOffset + arraySize * elementSize);
                // We can find arrays whose backing store has not been fully committed.
                return committedOnly && arraySizeInBytes > objectView.Size ? (int)objectView.Size : arraySizeInBytes;
            }
            else if (typeIndex == TypeSystem.SystemStringTypeIndex)
            {
                objectView.Read(TypeSystem.SystemStringLengthOffset, out int stringLength);
                return RoundToAllocationGranularity(TypeSystem.SystemStringFirstCharOffset + (stringLength + 1) * sizeof(char));
            }
            else
            {
                return RoundToAllocationGranularity(TypeSystem.BaseSize(typeIndex));
            }
        }

        public int GetElementSize(int elementTypeIndex)
        {
            // TODO: round up element size appropriately?
            return GetFieldSize(elementTypeIndex);
        }

        public int GetFieldSize(int typeIndex)
        {
            if (TypeSystem.IsValueType(typeIndex))
            {
                return TypeSystem.BaseSize(typeIndex);
            }
            else
            {
                return TypeSystem.PointerSize;
            }
        }

        public int RoundToAllocationGranularity(int size)
        {
            int insignificantBits = TypeSystem.AllocationGranularity - 1;
            return (size + insignificantBits) & ~insignificantBits;
        }

        public IEnumerable<int> GetObjectPointerOffsets(MemoryView objectView, int typeIndex)
        {
            if (TypeSystem.IsArray(typeIndex))
            {
                int elementTypeIndex = TypeSystem.BaseOrElementTypeIndex(typeIndex);
                objectView.Read(TypeSystem.ArraySizeOffsetInHeader, out int arraySize);
                int elementSize = GetElementSize(elementTypeIndex);
                int offsetOfFirstElement = TypeSystem.ArrayFirstElementOffset;
                for (int i = 0; i < arraySize; i++)
                {
                    foreach (int offset in GetFieldPointerOffsets(elementTypeIndex, offsetOfFirstElement + i * elementSize))
                    {
                        // We can find arrays whose backing store has not been fully committed.
                        if (offset + TypeSystem.PointerSize > objectView.Size)
                        {
                            break;
                        }

                        yield return offset;
                    }
                }
            }
            else
            {
                foreach (int offset in GetPointerOffsets(typeIndex, baseOffset: 0, hasHeader: true))
                {
                    yield return offset;
                }
            }
        }

        public IEnumerable<int> GetFieldPointerOffsets(int typeIndex, int baseOffset)
        {
            if (TypeSystem.IsValueType(typeIndex))
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
            int numberOfFields = TypeSystem.NumberOfFields(typeIndex);
            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                if (!TypeSystem.FieldIsStatic(typeIndex, fieldNumber))
                {
                    int fieldTypeIndex = TypeSystem.FieldType(typeIndex, fieldNumber);
                    int fieldOffset = TypeSystem.FieldOffset(typeIndex, fieldNumber);
                    if (!hasHeader)
                    {
                        if (fieldTypeIndex == typeIndex)
                        {
                            // Avoid infinite recursion due to the way that primitive types (such as System.Int32) are defined.
                            continue;
                        }
                        fieldOffset -= TypeSystem.ObjectHeaderSize;
                    }

                    foreach (int offset in GetFieldPointerOffsets(fieldTypeIndex, baseOffset + fieldOffset))
                    {
                        yield return offset;
                    }
                }
            }

            int baseOrElementTypeIndex = TypeSystem.BaseOrElementTypeIndex(typeIndex);
            if (baseOrElementTypeIndex >= 0)
            {
                foreach (int offset in GetPointerOffsets(baseOrElementTypeIndex, baseOffset, hasHeader))
                {
                    yield return offset;
                }
            }
        }
    }
}
