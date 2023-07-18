// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    enum TypeFlags : uint
    {
        None = 0,
        ValueType = 1 << 0,
        Array = 1 << 1,
        ArrayRankMask = 0xFFFF0000,
    }

    sealed class TypeDescription
    {
        public TypeFlags Flags;
        public string? Name;
        public string? Assembly;
        public MemoryView FieldIndices;
        public MemoryView StaticFieldBytes;
        public int BaseOrElementTypeIndex;
        public int Size;
        public NativeWord TypeInfoAddress;
        public int TypeIndex;
    }

    sealed class FieldDescription
    {
        public int Offset;
        public int TypeIndex;
        public string? Name;
        public bool IsStatic;
    }

    sealed class UnityManagedTypeSystem : TypeSystem
    {
        readonly TypeDescription[] m_typesByIndex;
        readonly FieldDescription[] m_fieldsByIndex;
        readonly VirtualMachineInformation m_virtualMachineInformation;
        readonly int m_stringTypeIndex;
        readonly int m_stringLengthOffset;
        readonly int m_stringFirstCharOffset;
        readonly int m_systemVoidStarTypeIndex;

        readonly TypeDescription[] m_typesByAddress;

        internal UnityManagedTypeSystem(
            TypeDescription[] types,
            FieldDescription[] fields,
            VirtualMachineInformation virtualMachineInformation,
            ReferenceClassifierFactory referenceClassifierFactory)
            : base(referenceClassifierFactory)
        {
            m_typesByIndex = types;
            m_fieldsByIndex = fields;
            m_stringTypeIndex = -1;
            m_systemVoidStarTypeIndex = -1;

            // Consistency check that the types are sorted by index and indices are contiguous.
            // Also, locate the System.String type and determine offset of length field/first char.
            for (int typeIndex = 0; typeIndex < m_typesByIndex.Length; typeIndex++)
            {
                if (m_typesByIndex[typeIndex].TypeIndex != typeIndex)
                {
                    throw new InvalidSnapshotFormatException("type indices not sorted and/or contiguous");
                }

                if (m_stringTypeIndex == -1
                    && IsInMscorlib(typeIndex)
                    && QualifiedName(typeIndex).Equals("System.String", StringComparison.Ordinal))
                {
                    m_stringTypeIndex = typeIndex;

                    int numberOfFields = NumberOfFields(typeIndex);
                    for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                    {
                        switch (FieldName(typeIndex, fieldNumber))
                        {
                            case "_stringLength":
                                m_stringLengthOffset = FieldOffset(typeIndex, fieldNumber, withHeader: true);
                                break;
                            case "_firstChar":
                                m_stringFirstCharOffset = FieldOffset(typeIndex, fieldNumber, withHeader: true);
                                break;
                        }
                    }
                }

                if (m_systemVoidStarTypeIndex == -1
                    && IsInMscorlib(typeIndex)
                    && QualifiedName(typeIndex).Equals("System.Void*", StringComparison.Ordinal))
                {
                    m_systemVoidStarTypeIndex = typeIndex;
                }
            }

            m_typesByAddress = new TypeDescription[types.Length];
            Array.Copy(m_typesByIndex, m_typesByAddress, types.Length);
            Array.Sort(m_typesByAddress, (type1, type2) => type1.TypeInfoAddress.Value.CompareTo(type2.TypeInfoAddress.Value));

            m_virtualMachineInformation = virtualMachineInformation;
        }

        bool IsInMscorlib(int typeIndex)
        {
            string assembly = Assembly(typeIndex);
            return assembly == "mscorlib" || assembly == "mscorlib.dll";
        }

        public override int PointerSize => m_virtualMachineInformation.PointerSize;

        public override int NumberOfTypeIndices => m_typesByIndex.Length;

        internal int TypeInfoAddressToIndex(NativeWord address)
        {
            int min = 0;
            int max = m_typesByIndex.Length;
            while (min < max)
            {
                int mid = (min + max) / 2;
                if (m_typesByAddress[mid].TypeInfoAddress == address)
                {
                    return m_typesByAddress[mid].TypeIndex;
                }
                else if (m_typesByAddress[mid].TypeInfoAddress < address)
                {
                    min = mid + 1;
                }
                else
                {
                    max = mid;
                }
            }

            return -1;
        }

        public override string Assembly(int typeIndex)
        {
            return m_typesByIndex[typeIndex].Assembly!;
        }

        // TODO: add a method to return an "assembly group", to be used for visualization
        // (one of {mscorlib,System[.*]}/{Unity[.*],UnityEngine[.*],UnityEditor[.*]}/other)

        public override string QualifiedName(int typeIndex)
        {
            return m_typesByIndex[typeIndex].Name!;
        }

        static readonly Regex s_identifierAndDotRegex = new("[a-zA-Z_][a-zA-Z0-9_]*\\.", RegexOptions.Compiled);

        public override string UnqualifiedName(int typeIndex)
        {
            string qualifiedName = QualifiedName(typeIndex);
            return s_identifierAndDotRegex.Replace(qualifiedName, string.Empty);
        }

        public override int BaseOrElementTypeIndex(int typeIndex)
        {
            return m_typesByIndex[typeIndex].BaseOrElementTypeIndex;
        }

        public override int ObjectHeaderSize(int typeIndex)
        {
            return m_virtualMachineInformation.ObjectHeaderSize;
        }

        public override int BaseSize(int typeIndex)
        {
            return m_typesByIndex[typeIndex].Size;
        }

        public override bool IsValueType(int typeIndex)
        {
            return (m_typesByIndex[typeIndex].Flags & TypeFlags.ValueType) != 0;
        }

        public override bool IsArray(int typeIndex)
        {
            return (m_typesByIndex[typeIndex].Flags & TypeFlags.Array) != 0;
        }

        public override int Rank(int typeIndex)
        {
            return (int)(m_typesByIndex[typeIndex].Flags & TypeFlags.ArrayRankMask) >> 16;
        }

        public override int NumberOfFields(int typeIndex)
        {
            MemoryView memoryView = m_typesByIndex[typeIndex].FieldIndices;
            return (int)(memoryView.Size / Marshal.SizeOf(typeof(int)));
        }

        int GetFieldIndex(int typeIndex, int fieldNumber)
        {
            MemoryView memoryView = m_typesByIndex[typeIndex].FieldIndices;
            memoryView.GetElementAtIndex(fieldNumber, Marshal.SizeOf(typeof(int))).Read(0, out int fieldIndex);
            return fieldIndex;
        }

        public override int FieldOffset(int typeIndex, int fieldNumber, bool withHeader)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].Offset
                - (withHeader ? 0 : m_virtualMachineInformation.ObjectHeaderSize);
        }

        public override int FieldType(int typeIndex, int fieldNumber)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].TypeIndex;
        }

        public override string FieldName(int typeIndex, int fieldNumber)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].Name!;
        }

        public override bool FieldIsStatic(int typeIndex, int fieldNumber)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].IsStatic;
        }

        public override MemoryView StaticFieldBytes(int typeIndex, int fieldNumber)
        {
            MemoryView memoryView = m_typesByIndex[typeIndex].StaticFieldBytes;
            if (memoryView.Size == 0)
            {
                // Type has not been initialized.
                return default;
            }
            else
            {
                int fieldTypeIndex = FieldType(typeIndex, fieldNumber);
                int size = IsValueType(fieldTypeIndex) ? BaseSize(fieldTypeIndex) : PointerSize;
                int fieldOffset = FieldOffset(typeIndex, fieldNumber, withHeader: true);
                if (fieldOffset == -1 || size < 0)
                {
                    // TODO: when can this happen?
                    return default;
                }

                return memoryView.GetRange(fieldOffset, size);
            }
        }

        internal int GetObjectSize(MemoryView objectView, int typeIndex, bool committedOnly)
        {
            if (IsArray(typeIndex))
            {
                int arraySize = ReadArraySize(objectView);
                int elementSize = GetArrayElementSize(BaseOrElementTypeIndex(typeIndex));
                int arraySizeInBytes = RoundToAllocationGranularity(m_virtualMachineInformation.ArrayHeaderSize + arraySize * elementSize);
                // We support arrays whose backing store has not been fully committed.
                return committedOnly && arraySizeInBytes > objectView.Size ? (int)objectView.Size : arraySizeInBytes;
            }
            else if (typeIndex == m_stringTypeIndex)
            {
                objectView.Read(m_stringLengthOffset, out int stringLength);
                return RoundToAllocationGranularity(m_stringFirstCharOffset + (stringLength + 1) * sizeof(char));
            }
            else
            {
                return RoundToAllocationGranularity(BaseSize(typeIndex));
            }
        }

        int RoundToAllocationGranularity(int size)
        {
            int insignificantBits = m_virtualMachineInformation.AllocationGranularity - 1;
            return (size + insignificantBits) & ~insignificantBits;
        }

        internal int ReadArraySize(MemoryView objectView)
        {
            objectView.Read(m_virtualMachineInformation.ArraySizeOffsetInHeader, out int arraySize);
            return arraySize;
        }

        public override int GetArrayElementOffset(int elementTypeIndex, int elementIndex)
        {
            return m_virtualMachineInformation.ArrayHeaderSize + elementIndex * GetArrayElementSize(elementTypeIndex);
        }

        public override int GetArrayElementSize(int elementTypeIndex)
        {
            // TODO: round up element size appropriately?
            return GetFieldSize(elementTypeIndex);
        }

        int GetFieldSize(int typeIndex)
        {
            if (IsValueType(typeIndex))
            {
                return BaseSize(typeIndex);
            }
            else
            {
                return PointerSize;
            }
        }

        public override int SystemStringTypeIndex => m_stringTypeIndex;

        public override int SystemStringLengthOffset => m_stringLengthOffset;

        public override int SystemStringFirstCharOffset => m_stringFirstCharOffset;

        public override int SystemVoidStarTypeIndex => m_systemVoidStarTypeIndex;

        public override IEnumerable<string> DumpStats()
        {
            yield return string.Format("Pointer size: {0}", PointerSize);
            yield return string.Format("Object header size: {0}", m_virtualMachineInformation.ObjectHeaderSize);
            yield return string.Format("Array header size: {0}", m_virtualMachineInformation.ArrayHeaderSize);
            yield return string.Format("Array bounds offset in header: {0}", m_virtualMachineInformation.ArrayBoundsOffsetInHeader);
            yield return string.Format("Array size offset in header: {0}", m_virtualMachineInformation.ArraySizeOffsetInHeader);
            yield return string.Format("Allocation granularity: {0}", m_virtualMachineInformation.AllocationGranularity);
            yield return string.Empty;
            yield return string.Format("Number of types: {0}", NumberOfTypeIndices);
            yield return string.Empty;
            yield return string.Format("System.String type index: {0}", SystemStringTypeIndex);
            yield return string.Format("System.String length offset: {0}", SystemStringLengthOffset);
            yield return string.Format("System.String first char offset: {0}", SystemStringFirstCharOffset);
        }
    }
}
