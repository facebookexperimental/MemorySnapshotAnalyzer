// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class StitchedTypeSystem : ITypeSystem
    {
        readonly int m_pointerSize;
        readonly ITypeSystem m_first;
        readonly ITypeSystem m_second;
        readonly int m_systemStringTypeIndex;
        readonly int m_systemStringLengthOffset;
        readonly int m_systemStringFirstCharOffset;

        public StitchedTypeSystem(ITypeSystem first, ITypeSystem second)
        {
            if (first.PointerSize != second.PointerSize)
            {
                throw new ArgumentException();
            }
            m_pointerSize = first.PointerSize;

            if (first.SystemStringTypeIndex != -1 && second.SystemStringTypeIndex != -1)
            {
                throw new ArgumentException();
            }

            m_first = first;
            m_second = second;

            if (first.SystemStringTypeIndex != -1)
            {
                m_systemStringTypeIndex = first.SystemStringTypeIndex;
                m_systemStringLengthOffset = first.SystemStringLengthOffset;
                m_systemStringFirstCharOffset = first.SystemStringFirstCharOffset;
            }
            else if (second.SystemStringTypeIndex != -1)
            {
                m_systemStringTypeIndex = second.SystemStringTypeIndex;
                m_systemStringLengthOffset = second.SystemStringLengthOffset;
                m_systemStringFirstCharOffset = second.SystemStringFirstCharOffset;
            }
            else
            {
                m_systemStringTypeIndex = -1;
            }
        }

        public int PointerSize => m_pointerSize;

        public int NumberOfTypeIndices => m_first.NumberOfTypeIndices + m_second.NumberOfTypeIndices;

        public string Assembly(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.Assembly(typeIndex);
            }
            else
            {
                return m_second.Assembly(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public string QualifiedName(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.QualifiedName(typeIndex);
            }
            else
            {
                return m_second.QualifiedName(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public string UnqualifiedName(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.UnqualifiedName(typeIndex);
            }
            else
            {
                return m_second.UnqualifiedName(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public int BaseOrElementTypeIndex(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.BaseOrElementTypeIndex(typeIndex);
            }
            else
            {
                return m_second.BaseOrElementTypeIndex(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public int BaseSize(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.BaseSize(typeIndex);
            }
            else
            {
                return m_second.BaseSize(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public bool IsValueType(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.IsValueType(typeIndex);
            }
            else
            {
                return m_second.IsValueType(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public bool IsArray(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.IsArray(typeIndex);
            }
            else
            {
                return m_second.IsArray(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public int Rank(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.Rank(typeIndex);
            }
            else
            {
                return m_second.Rank(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public int NumberOfFields(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.NumberOfFields(typeIndex);
            }
            else
            {
                return m_second.NumberOfFields(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public int FieldOffset(int typeIndex, int fieldNumber, bool hasHeader)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.FieldOffset(typeIndex, fieldNumber, hasHeader);
            }
            else
            {
                return m_second.FieldOffset(typeIndex - m_first.NumberOfTypeIndices, fieldNumber, hasHeader);
            }
        }

        public int FieldType(int typeIndex, int fieldNumber)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.FieldType(typeIndex, fieldNumber);
            }
            else
            {
                return m_second.FieldType(typeIndex - m_first.NumberOfTypeIndices, fieldNumber);
            }
        }

        public string FieldName(int typeIndex, int fieldNumber)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.FieldName(typeIndex, fieldNumber);
            }
            else
            {
                return m_second.FieldName(typeIndex - m_first.NumberOfTypeIndices, fieldNumber);
            }
        }

        public bool FieldIsStatic(int typeIndex, int fieldNumber)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.FieldIsStatic(typeIndex, fieldNumber);
            }
            else
            {
                return m_second.FieldIsStatic(typeIndex - m_first.NumberOfTypeIndices, fieldNumber);
            }
        }

        public int GetArrayElementOffset(int elementTypeIndex, int elementIndex)
        {
            if (elementTypeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.GetArrayElementOffset(elementTypeIndex, elementIndex);
            }
            else
            {
                return m_second.GetArrayElementOffset(elementTypeIndex - m_first.NumberOfTypeIndices, elementIndex);
            }
        }

        public int GetArrayElementSize(int elementTypeIndex)
        {
            if (elementTypeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.GetArrayElementSize(elementTypeIndex);
            }
            else
            {
                return m_second.GetArrayElementSize(elementTypeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public int SystemStringTypeIndex => m_systemStringTypeIndex;

        public int SystemStringLengthOffset => m_systemStringLengthOffset;

        public int SystemStringFirstCharOffset => m_systemStringFirstCharOffset;

        public IEnumerable<string> DumpStats()
        {
            foreach (var s in m_first.DumpStats())
            {
                yield return s;
            }

            foreach (var s in m_second.DumpStats())
            {
                yield return s;
            }
        }
    }
}
