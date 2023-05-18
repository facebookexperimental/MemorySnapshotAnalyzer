// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class TypeSystem
    {
        public abstract int PointerSize { get; }

        public abstract int NumberOfTypeIndices { get; }

        public abstract string Assembly(int typeIndex);

        public abstract string QualifiedName(int typeIndex);

        public abstract string UnqualifiedName(int typeIndex);

        public abstract int BaseOrElementTypeIndex(int typeIndex);

        public abstract int BaseSize(int typeIndex);

        public abstract bool IsValueType(int typeIndex);

        public abstract bool IsArray(int typeIndex);

        public abstract int Rank(int typeIndex);

        public abstract int NumberOfFields(int typeIndex);

        public abstract int FieldOffset(int typeIndex, int fieldNumber, bool withHeader);

        public abstract int FieldType(int typeIndex, int fieldNumber);

        public abstract string FieldName(int typeIndex, int fieldNumber);

        public abstract bool FieldIsStatic(int typeIndex, int fieldNumber);

        public abstract MemoryView StaticFieldBytes(int typeIndex, int fieldNumber);

        public abstract int GetArrayElementOffset(int elementTypeIndex, int elementIndex);

        public abstract int GetArrayElementSize(int elementTypeIndex);

        public abstract int SystemStringTypeIndex { get; }

        public abstract int SystemStringLengthOffset { get; }

        public abstract int SystemStringFirstCharOffset { get; }

        public abstract IEnumerable<string> DumpStats();

        public IEnumerable<int> GetFieldPointerOffsets(int typeIndex, int baseOffset)
        {
            if (IsValueType(typeIndex))
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

        public IEnumerable<int> GetPointerOffsets(int typeIndex, int baseOffset, bool hasHeader)
        {
            int numberOfFields = NumberOfFields(typeIndex);
            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                if (!FieldIsStatic(typeIndex, fieldNumber))
                {
                    int fieldTypeIndex = FieldType(typeIndex, fieldNumber);
                    int fieldOffset = FieldOffset(typeIndex, fieldNumber, withHeader: hasHeader);
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

            int baseOrElementTypeIndex = BaseOrElementTypeIndex(typeIndex);
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
