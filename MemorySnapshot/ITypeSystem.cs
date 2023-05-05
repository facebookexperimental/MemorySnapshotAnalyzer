namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public interface ITypeSystem
    {
        int PointerSize { get; }

        int VTableOffsetInHeader { get; }

        int ObjectHeaderSize { get; }

        int ArrayHeaderSize { get; }

        int ArrayBoundsOffsetInHeader { get; }

        int ArraySizeOffsetInHeader { get; }

        int ArrayFirstElementOffset { get; }

        int AllocationGranularity { get; }

        int NumberOfTypeIndices { get; }

        int TypeInfoAddressToIndex(NativeWord address);

        NativeWord TypeInfoAddress(int typeIndex);

        string Assembly(int typeIndex);

        string QualifiedName(int typeIndex);

        string UnqualifiedName(int typeIndex);

        int BaseOrElementTypeIndex(int typeIndex);

        int BaseSize(int typeIndex);

        bool IsValueType(int typeIndex);

        bool IsArray(int typeIndex);

        int Rank(int typeIndex);

        int NumberOfFields(int typeIndex);

        int FieldOffset(int typeIndex, int fieldNumber);

        int FieldType(int typeIndex, int fieldNumber);

        string FieldName(int typeIndex, int fieldNumber);

        bool FieldIsStatic(int typeIndex, int fieldNumber);

        MemoryView StaticFieldBytes(int typeIndex, int fieldNumber);

        int SystemStringTypeIndex { get; }

        int SystemStringLengthOffset { get; }

        int SystemStringFirstCharOffset { get; }
    }
}
