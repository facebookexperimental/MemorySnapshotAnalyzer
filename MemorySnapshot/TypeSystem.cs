// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class TypeSystem
    {
        readonly ReferenceClassifierFactory m_referenceClassifierFactory;
        readonly List<PointerInfo<int>> m_offsets;
        readonly Dictionary<int, (int, int)> m_typeIndexToIndexAndCount;

        ReferenceClassifier? m_referenceClassifier;

        protected TypeSystem(ReferenceClassifierFactory referenceClassifierFactory)
        {
            m_referenceClassifierFactory = referenceClassifierFactory;
            m_offsets = new List<PointerInfo<int>>();
            m_typeIndexToIndexAndCount = new Dictionary<int, (int, int)>();
        }

        public abstract int PointerSize { get; }

        public abstract int NumberOfTypeIndices { get; }

        public abstract string Assembly(int typeIndex);

        public abstract string QualifiedName(int typeIndex);

        public abstract string UnqualifiedName(int typeIndex);

        public abstract int BaseOrElementTypeIndex(int typeIndex);

        public abstract int ObjectHeaderSize(int typeIndex);

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

        public abstract int SystemVoidStarTypeIndex { get; }

        public abstract IEnumerable<string> DumpStats();

        ValueTuple<int, int> EnsurePointerOffsets(int typeIndex)
        {
            if (m_typeIndexToIndexAndCount.TryGetValue(typeIndex, out (int, int) pair))
            {
                return pair;
            }
            else
            {
                int start = m_offsets.Count;
                ComputePointerOffsets(typeIndex, baseOffset: 0);
                int end = m_offsets.Count;
                m_typeIndexToIndexAndCount.Add(typeIndex, (start, end));
                return (start, end);
            }
        }

        void ComputePointerOffsets(int typeIndex, int baseOffset)
        {
            int baseOrElementTypeIndex = BaseOrElementTypeIndex(typeIndex);
            if (baseOrElementTypeIndex >= 0)
            {
                ComputePointerOffsets(baseOrElementTypeIndex, baseOffset);
            }

            int numberOfFields = NumberOfFields(typeIndex);
            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                if (!FieldIsStatic(typeIndex, fieldNumber))
                {
                    int fieldTypeIndex = FieldType(typeIndex, fieldNumber);
                    int fieldOffset = FieldOffset(typeIndex, fieldNumber, withHeader: false);

                    if (IsValueType(fieldTypeIndex))
                    {
                        // Avoid infinite recursion due to the way that primitive types (such as System.Int32) are defined.
                        if (fieldTypeIndex != typeIndex)
                        {
                            ComputePointerOffsets(fieldTypeIndex, baseOffset + fieldOffset);
                        }
                    }
                    else
                    {
                        PointerFlags pointerFlags = ClassifyField(typeIndex, fieldNumber);
                        m_offsets.Add(new PointerInfo<int>
                        {
                            Value = baseOffset + fieldOffset,
                            PointerFlags = pointerFlags,
                            TypeIndex = typeIndex,
                            FieldNumber = fieldNumber
                        });
                    }
                }
            }
        }

        PointerFlags ClassifyField(int typeIndex, int fieldNumber)
        {
            if (m_referenceClassifier == null)
            {
                m_referenceClassifier = m_referenceClassifierFactory.Build(this);
            }

            PointerFlags pointerFlags = PointerFlags.None;
            if (m_referenceClassifier.IsOwningReference(typeIndex, fieldNumber))
            {
                pointerFlags |= PointerFlags.IsOwningReference;
            }

            if (m_referenceClassifier.IsConditionAnchor(typeIndex, fieldNumber))
            {
                pointerFlags |= PointerFlags.IsConditionAnchor;
            }

            return pointerFlags;
        }

        public IEnumerable<PointerInfo<int>> GetPointerOffsets(int typeIndex, int baseOffset)
        {
            (int start, int end) = EnsurePointerOffsets(typeIndex);

            for (int i = start; i < end; i++)
            {
                PointerInfo<int> pointerInfo = m_offsets[i];
                yield return pointerInfo.WithValue(pointerInfo.Value + baseOffset);
            }
        }

        public IEnumerable<PointerInfo<int>> GetStaticFieldPointerOffsets(int typeIndex, int fieldNumber, int fieldTypeIndex, int baseOffset)
        {
            if (IsValueType(fieldTypeIndex))
            {
                foreach (PointerInfo<int> pointerInfo in GetPointerOffsets(fieldTypeIndex, baseOffset))
                {
                    yield return pointerInfo;
                }
            }
            else
            {
                PointerFlags pointerFlags = ClassifyField(typeIndex, fieldNumber);
                yield return new PointerInfo<int>
                {
                    Value = baseOffset,
                    PointerFlags = pointerFlags,
                    TypeIndex = typeIndex,
                    FieldNumber = fieldNumber
                };
            }
        }

        public IEnumerable<PointerInfo<int>> GetArrayElementPointerOffsets(int typeIndex, int baseOffset)
        {
            if (IsValueType(typeIndex))
            {
                foreach (PointerInfo<int> pointerInfo in GetPointerOffsets(typeIndex, baseOffset))
                {
                    yield return pointerInfo;
                }
            }
            else
            {
                yield return new PointerInfo<int>
                {
                    Value = baseOffset,
                    PointerFlags = PointerFlags.None,
                    TypeIndex = typeIndex,
                    FieldNumber = -1
                };
            }
        }

        public int GetFieldNumber(int typeIndex, string fieldName)
        {
            int numberOfFields = NumberOfFields(typeIndex);
            for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
            {
                string aFieldName = FieldName(typeIndex, fieldNumber);
                if (aFieldName.Equals(fieldName, StringComparison.Ordinal))
                {
                    return fieldNumber;
                }
            }

            return -1;
        }

        public List<(int offset, int typeIndex)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
        {
            return m_referenceClassifier!.GetConditionalAnchorFieldPaths(typeIndex, fieldNumber);
        }
    }
}
