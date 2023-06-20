// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.UnityBackend
{
    sealed class NativeType
    {
        public string? Name;
        public int BaseTypeIndex;
    }

    sealed class UnityNativeObjectTypeSystem : TypeSystem
    {
        readonly int m_pointerSize;
        readonly NativeType[] m_nativeTypes;

        internal UnityNativeObjectTypeSystem(int pointerSize, NativeType[] nativeTypes, ReferenceClassifierFactory referenceClassifierFactory)
            : base(referenceClassifierFactory)
        {
            m_pointerSize = pointerSize;
            m_nativeTypes = nativeTypes;
        }

        public override int PointerSize => m_pointerSize;

        public override int NumberOfTypeIndices => m_nativeTypes.Length;

        public override string Assembly(int typeIndex)
        {
            return string.Empty;
        }

        public override string QualifiedName(int typeIndex)
        {
            return m_nativeTypes[typeIndex].Name!;
        }

        public override string UnqualifiedName(int typeIndex)
        {
            return m_nativeTypes[typeIndex].Name!;
        }

        public override int BaseOrElementTypeIndex(int typeIndex)
        {
            return m_nativeTypes[typeIndex].BaseTypeIndex;
        }

        public override int ObjectHeaderSize(int typeIndex)
        {
            throw new NotImplementedException();
        }

        public override int BaseSize(int typeIndex)
        {
            // Can be output by dumptype
            return 0;
        }

        public override bool IsValueType(int typeIndex)
        {
            return false;
        }

        public override bool IsArray(int typeIndex)
        {
            return false;
        }

        public override int Rank(int typeIndex)
        {
            return 0;
        }

        public override int NumberOfFields(int typeIndex)
        {
            return 0;
        }

        public override int FieldOffset(int typeIndex, int fieldNumber, bool withHeader)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public override int FieldType(int typeIndex, int fieldNumber)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public override string FieldName(int typeIndex, int fieldNumber)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public override bool FieldIsStatic(int typeIndex, int fieldNumber)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException();
        }

        public override MemoryView StaticFieldBytes(int typeIndex, int fieldNumber)
        {
            // Unreachable - no type has fields
            throw new NotImplementedException(); ;
        }

        public override int GetArrayElementOffset(int elementTypeIndex, int elementIndex)
        {
            // Unreachable - for no type does IsArray return true
            throw new NotImplementedException();
        }

        public override int GetArrayElementSize(int elementTypeIndex)
        {
            // Unreachable - for no type does IsArray return true
            throw new NotImplementedException();
        }

        public override int SystemStringTypeIndex => -1;

        // Unreachable - no string type
        public override int SystemStringLengthOffset => throw new NotImplementedException();

        // Unreachable - no string type
        public override int SystemStringFirstCharOffset => throw new NotImplementedException();

        public override IEnumerable<string> DumpStats()
        {
            yield return string.Format("Pointer size: {0}", PointerSize);
            yield return string.Format("Number of types: {0}", NumberOfTypeIndices);
        }
    }
}
