/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class TypeSystem
    {
        readonly ReferenceClassifierFactory m_referenceClassifierFactory;
        readonly List<PointerInfo<int>> m_offsets;
        readonly Dictionary<int, (int, int)> m_typeIndexToIndexAndCount;
        readonly Dictionary<int, string> m_typeIndexToGenericNameWithArity;

        ReferenceClassifier? m_referenceClassifier;

        protected TypeSystem(ReferenceClassifierFactory referenceClassifierFactory)
        {
            m_referenceClassifierFactory = referenceClassifierFactory;
            m_offsets = new List<PointerInfo<int>>();
            m_typeIndexToIndexAndCount = new Dictionary<int, (int, int)>();
            m_typeIndexToGenericNameWithArity = new();
        }

        public abstract int PointerSize { get; }

        public abstract int NumberOfTypeIndices { get; }

        public abstract string Assembly(int typeIndex);

        public abstract string QualifiedName(int typeIndex);

        public string QualifiedGenericNameWithArity(int typeIndex)
        {
            if (m_typeIndexToGenericNameWithArity.TryGetValue(typeIndex, out string? qualifiedGenericNameWithArity))
            {
                return qualifiedGenericNameWithArity;
            }

            string qualifiedName = QualifiedName(typeIndex);
            int indexOfLeftAngle = qualifiedName.IndexOf('<');
            if (indexOfLeftAngle == -1 || qualifiedName.Length == 0 || qualifiedName[^1] != '>')
            {
                m_typeIndexToGenericNameWithArity.Add(typeIndex, qualifiedName);
                return qualifiedName;
            }

            ReadOnlySpan<char> qualifiedGenericName = qualifiedName.AsSpan()[..indexOfLeftAngle];
            int arity = ComputeArity(qualifiedName.AsSpan()[(indexOfLeftAngle + 1)..^1]);
            qualifiedGenericNameWithArity = $"{qualifiedGenericName}`{arity}";
            m_typeIndexToGenericNameWithArity.Add(typeIndex, qualifiedGenericNameWithArity);
            return qualifiedGenericNameWithArity;
        }

        static int ComputeArity(ReadOnlySpan<char> genericArguments)
        {
            int depth = 0, arity = 1;
            for (int i = 0; i < genericArguments.Length; i++)
            {
                switch (genericArguments[i])
                {
                    case '<':
                        depth++;
                        break;
                    case '>':
                        depth--;
                        break;
                    case ',':
                        if (depth == 0)
                        {
                            arity++;
                        }
                        break;
                }
            }
            return arity;
        }

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

                    bool isValueType = IsValueType(fieldTypeIndex);
                    PointerFlags pointerFlags = ReferenceClassifier.GetPointerFlags(typeIndex, fieldNumber);
                    if (!isValueType || pointerFlags != PointerFlags.None)
                    {
                        m_offsets.Add(new PointerInfo<int>
                        {
                            Value = baseOffset + fieldOffset,
                            PointerFlags = pointerFlags,
                            TypeIndex = typeIndex,
                            FieldNumber = fieldNumber
                        });
                    }

                    // Avoid infinite recursion due to the way that primitive types (such as System.Int32) are defined.
                    if (isValueType && fieldTypeIndex != typeIndex)
                    {
                        ComputePointerOffsets(fieldTypeIndex, baseOffset + fieldOffset);
                    }
                }
            }
        }

        ReferenceClassifier ReferenceClassifier
        {
            get
            {
                if (m_referenceClassifier == null)
                {
                    m_referenceClassifier = m_referenceClassifierFactory.Build(this);
                }
                return m_referenceClassifier;
            }
        }

        public IEnumerable<PointerInfo<int>> GetPointerOffsets(int typeIndex, int baseOffset)
        {
            Debug.Assert(!IsArray(typeIndex));

            (int start, int end) = EnsurePointerOffsets(typeIndex);

            for (int i = start; i < end; i++)
            {
                PointerInfo<int> pointerInfo = m_offsets[i];
                yield return pointerInfo.WithValue(pointerInfo.Value + baseOffset);
            }
        }

        public IEnumerable<PointerInfo<int>> GetStaticFieldPointerOffsets(int typeIndex, int fieldNumber)
        {
            Debug.Assert(FieldIsStatic(typeIndex, fieldNumber));

            int fieldTypeIndex = FieldType(typeIndex, fieldNumber);
            if (IsValueType(fieldTypeIndex))
            {
                foreach (PointerInfo<int> pointerInfo in GetPointerOffsets(fieldTypeIndex, baseOffset: 0))
                {
                    yield return pointerInfo;
                }
            }
            else
            {
                PointerFlags pointerFlags = ReferenceClassifier.GetPointerFlags(typeIndex, fieldNumber);
                yield return new PointerInfo<int>
                {
                    Value = 0,
                    PointerFlags = pointerFlags,
                    TypeIndex = typeIndex,
                    FieldNumber = fieldNumber
                };
            }
        }

        public IEnumerable<PointerInfo<int>> GetArrayElementPointerOffsets(int elementTypeIndex, int baseOffset)
        {
            if (IsValueType(elementTypeIndex))
            {
                foreach (PointerInfo<int> pointerInfo in GetPointerOffsets(elementTypeIndex, baseOffset))
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
                    TypeIndex = elementTypeIndex,
                    FieldNumber = -1
                };
            }
        }

        public (int typeIndex, int fieldNumber) GetFieldNumber(int typeIndex, string fieldName)
        {
            if (IsArray(typeIndex))
            {
                return (-1, -1);
            }
            else if (IsValueType(typeIndex))
            {
                int fieldNumber = GetOwnFieldNumber(typeIndex, fieldName);
                if (fieldNumber != -1)
                {
                    return (typeIndex, fieldNumber);
                }
            }
            else
            {
                int currentTypeIndex = typeIndex;
                do
                {
                    int fieldNumber = GetOwnFieldNumber(currentTypeIndex, fieldName);
                    if (fieldNumber != -1)
                    {
                        return (currentTypeIndex, fieldNumber);
                    }
                    currentTypeIndex = BaseOrElementTypeIndex(currentTypeIndex);
                }
                while (currentTypeIndex != -1);
            }

            return (-1, -1);
        }

        int GetOwnFieldNumber(int typeIndex, string fieldName)
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

        public IEnumerable<Selector> GetConditionAnchorSelectors(int typeIndex, int fieldNumber)
        {
            return m_referenceClassifier!.GetConditionAnchorSelectors(typeIndex, fieldNumber);
        }

        public IEnumerable<(Selector selector, List<string> tags)> GetTagAnchorSelectors(int typeIndex, int fieldNumber)
        {
            return m_referenceClassifier!.GetTagAnchorSelectors(typeIndex, fieldNumber);
        }

        public (List<string> zeroTags, List<string> nonZeroTags) GetTags(int typeIndex, int fieldNumber)
        {
            return m_referenceClassifier!.GetTags(typeIndex, fieldNumber);
        }
    }
}
