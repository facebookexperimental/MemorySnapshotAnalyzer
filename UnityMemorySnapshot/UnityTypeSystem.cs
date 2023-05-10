// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
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

    sealed class UnityTypeSystem : ITypeSystem
    {
        readonly TypeDescription[] m_typesByIndex;
        readonly FieldDescription[] m_fieldsByIndex;
        readonly VirtualMachineInformation m_virtualMachineInformation;
        readonly int m_stringTypeIndex;
        readonly int m_stringLengthOffset;
        readonly int m_stringFirstCharOffset;

        readonly TypeDescription[] m_typesByAddress;

        internal UnityTypeSystem(TypeDescription[] types, FieldDescription[] fields, VirtualMachineInformation virtualMachineInformation)
        {
            m_typesByIndex = types;
            m_fieldsByIndex = fields;
            m_stringTypeIndex = -1;

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
                    && ((ITypeSystem)this).QualifiedName(typeIndex) == "System.String")
                {
                    m_stringTypeIndex = typeIndex;

                    MemoryView memoryView = m_typesByIndex[typeIndex].FieldIndices;
                    int numberOfFields = (int)(memoryView.Size / Marshal.SizeOf(typeof(int)));
                    for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                    {
                        memoryView.GetElementAtIndex(fieldNumber, Marshal.SizeOf(typeof(int))).Read(0, out int fieldIndex);
                        switch (fields[fieldIndex].Name!)
                        {
                            case "_stringLength":
                                m_stringLengthOffset = fields[fieldIndex].Offset;
                                break;
                            case "_firstChar":
                                m_stringFirstCharOffset = fields[fieldIndex].Offset;
                                break;
                        }
                    }
                }
            }

            m_typesByAddress = new TypeDescription[types.Length];
            Array.Copy(m_typesByIndex, m_typesByAddress, types.Length);
            Array.Sort(m_typesByAddress, (type1, type2) => type1.TypeInfoAddress.Value.CompareTo(type2.TypeInfoAddress.Value));

            m_virtualMachineInformation = virtualMachineInformation;
        }

        bool IsInMscorlib(int typeIndex)
        {
            string assembly = ((ITypeSystem)this).Assembly(typeIndex);
            return assembly == "mscorlib" || assembly == "mscorlib.dll";
        }

        public int PointerSize => m_virtualMachineInformation.PointerSize;

        public int VTableOffsetInHeader => 0;

        public int ObjectHeaderSize => m_virtualMachineInformation.ObjectHeaderSize;

        public int ArrayHeaderSize => m_virtualMachineInformation.ArrayHeaderSize;

        public int ArrayBoundsOffsetInHeader => m_virtualMachineInformation.ArrayBoundsOffsetInHeader;

        public int ArraySizeOffsetInHeader => m_virtualMachineInformation.ArraySizeOffsetInHeader;

        public int ArrayFirstElementOffset => (ArraySizeOffsetInHeader + sizeof(int) + 7) & ~7;

        public int AllocationGranularity => m_virtualMachineInformation.AllocationGranularity;

        public int NumberOfTypeIndices => m_typesByIndex.Length;

        public int TypeInfoAddressToIndex(NativeWord address)
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

        public NativeWord TypeInfoAddress(int typeIndex)
        {
            return m_typesByIndex[typeIndex].TypeInfoAddress;
        }

        public string Assembly(int typeIndex)
        {
            return m_typesByIndex[typeIndex].Assembly!;
        }

        // TODO: add a method to return an "assembly group", to be used for visualization
        // (one of {mscorlib,System[.*]}/{Unity[.*],UnityEngine[.*],UnityEditor[.*]}/other)

        public string QualifiedName(int typeIndex)
        {
            return m_typesByIndex[typeIndex].Name!;
        }

        static readonly Regex s_identifierAndDotRegex = new("[a-zA-Z_][a-zA-Z0-9_]*\\.", RegexOptions.Compiled);

        public string UnqualifiedName(int typeIndex)
        {
            string qualifiedName = QualifiedName(typeIndex);
            return s_identifierAndDotRegex.Replace(qualifiedName, string.Empty);
        }

        public int BaseOrElementTypeIndex(int typeIndex)
        {
            return m_typesByIndex[typeIndex].BaseOrElementTypeIndex;
        }

        public int BaseSize(int typeIndex)
        {
            return m_typesByIndex[typeIndex].Size;
        }

        public bool IsValueType(int typeIndex)
        {
            return (m_typesByIndex[typeIndex].Flags & TypeFlags.ValueType) != 0;
        }

        public bool IsArray(int typeIndex)
        {
            return (m_typesByIndex[typeIndex].Flags & TypeFlags.Array) != 0;
        }

        public int Rank(int typeIndex)
        {
            return (int)(m_typesByIndex[typeIndex].Flags & TypeFlags.ArrayRankMask) >> 16;
        }

        public int NumberOfFields(int typeIndex)
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

        public int FieldOffset(int typeIndex, int fieldNumber)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].Offset;
        }

        public int FieldType(int typeIndex, int fieldNumber)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].TypeIndex;
        }

        public string FieldName(int typeIndex, int fieldNumber)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].Name!;
        }

        public bool FieldIsStatic(int typeIndex, int fieldNumber)
        {
            return m_fieldsByIndex[GetFieldIndex(typeIndex, fieldNumber)].IsStatic;
        }

        public MemoryView StaticFieldBytes(int typeIndex, int fieldNumber)
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
                int fieldOffset = FieldOffset(typeIndex, fieldNumber);
                if (fieldOffset == -1 || size < 0)
                {
                    // TODO: when can this happen?
                    return default;
                }

                return memoryView.GetRange(fieldOffset, size);
            }
        }

        int ITypeSystem.SystemStringTypeIndex => m_stringTypeIndex;

        int ITypeSystem.SystemStringLengthOffset => m_stringLengthOffset;

        int ITypeSystem.SystemStringFirstCharOffset => m_stringFirstCharOffset;
    }
}
