using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class MemorySnapshot : IDisposable
    {
        protected MemorySnapshot() {}

        public abstract void Dispose();

        public abstract string Filename { get; }

        public abstract Native Native { get; }

        public abstract ManagedHeap ManagedHeap { get; }

        public abstract ITypeSystem TypeSystem { get; }

        public abstract int NumberOfGCHandles { get; }

        public abstract NativeWord GCHandleTarget(int gcHandleIndex);

        public MemoryView GetMemoryViewForAddress(NativeWord address)
        {
            ManagedHeapSegment? segment = ManagedHeap.GetSegmentForAddress(address);
            if (segment == null)
            {
                return default;
            }

            long offset = (long)(address - segment.StartAddress).Value;
            return segment.MemoryView.GetRange(offset, segment.Size - offset);
        }

        public int TryGetTypeIndex(NativeWord address)
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
            // TODO: round up element size appropriately, or get from objectView.Il2CppClass.element_size
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
