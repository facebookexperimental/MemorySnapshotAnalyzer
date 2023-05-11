// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public interface ITypeSystem
    {
        int PointerSize { get; }

        int NumberOfTypeIndices { get; }

        string Assembly(int typeIndex);

        string QualifiedName(int typeIndex);

        string UnqualifiedName(int typeIndex);

        int BaseOrElementTypeIndex(int typeIndex);

        int BaseSize(int typeIndex);

        bool IsValueType(int typeIndex);

        bool IsArray(int typeIndex);

        int Rank(int typeIndex);

        int NumberOfFields(int typeIndex);

        int FieldOffset(int typeIndex, int fieldNumber, bool withHeader);

        int FieldType(int typeIndex, int fieldNumber);

        string FieldName(int typeIndex, int fieldNumber);

        bool FieldIsStatic(int typeIndex, int fieldNumber);

        MemoryView StaticFieldBytes(int typeIndex, int fieldNumber);

        int GetObjectSize(MemoryView objectView, int typeIndex, bool committedOnly);

        int ReadArraySize(MemoryView objectView);

        int GetArrayElementOffset(int elementTypeIndex, int elementIndex);

        int GetArrayElementSize(int elementTypeIndex);

        int SystemStringTypeIndex { get; }

        int SystemStringLengthOffset { get; }

        int SystemStringFirstCharOffset { get; }

        IEnumerable<string> DumpStats();

        string? DescribeAddress(NativeWord address);
    }
}
