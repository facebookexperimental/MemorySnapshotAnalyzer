// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class NativeType
    {
        public string? Name;
        public int BaseTypeIndex;
    }

    sealed class UnityNativeObjectTypeSystem : ITypeSystem
    {
        readonly int m_pointerSize;
        readonly NativeType[] m_nativeTypes;

        internal UnityNativeObjectTypeSystem(int pointerSize, NativeType[] nativeTypes)
        {
            m_pointerSize = pointerSize;
            m_nativeTypes = nativeTypes;
        }

        public int PointerSize => m_pointerSize;

        public int NumberOfTypeIndices => m_nativeTypes.Length;

        public string Assembly(int typeIndex)
        {
            return string.Empty;
        }

        public string QualifiedName(int typeIndex)
        {
            return m_nativeTypes[typeIndex].Name!;
        }

        public string UnqualifiedName(int typeIndex)
        {
            return m_nativeTypes[typeIndex].Name!;
        }

        public int BaseOrElementTypeIndex(int typeIndex)
        {
            return m_nativeTypes[typeIndex].BaseTypeIndex;
        }

        public int BaseSize(int typeIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsValueType(int typeIndex)
        {
            return false;
        }

        public bool IsArray(int typeIndex)
        {
            return false;
        }

        public int Rank(int typeIndex)
        {
            return 0;
        }

        public int NumberOfFields(int typeIndex)
        {
            return 0;
        }

        public int FieldOffset(int typeIndex, int fieldNumber, bool hasHeader)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public int FieldType(int typeIndex, int fieldNumber)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public string FieldName(int typeIndex, int fieldNumber)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public bool FieldIsStatic(int typeIndex, int fieldNumber)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public int GetArrayElementOffset(int elementTypeIndex, int elementIndex)
        {
            // Unreachable - for no type does IsArray return true
            throw new NotImplementedException();
        }

        public int GetArrayElementSize(int elementTypeIndex)
        {
            // Unreachable - for no type does IsArray return true
            throw new NotImplementedException();
        }

        public int SystemStringTypeIndex => -1;

        // Unreachable - no string type
        public int SystemStringLengthOffset => throw new NotImplementedException();

        // Unreachable - no string type
        public int SystemStringFirstCharOffset => throw new NotImplementedException();

        public IEnumerable<string> DumpStats()
        {
            yield return string.Format("Pointer size: {0}", PointerSize);
            yield return string.Format("Number of types: {0}", NumberOfTypeIndices);
        }
    }
}
