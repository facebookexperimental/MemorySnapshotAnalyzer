// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class StitchedTypeSystem : TypeSystem
    {
        readonly int m_pointerSize;
        readonly TypeSystem m_first;
        readonly TypeSystem m_second;
        readonly int m_systemStringTypeIndex;
        readonly int m_systemStringLengthOffset;
        readonly int m_systemStringFirstCharOffset;
        readonly int m_systemVoidStarTypeIndex;

        public StitchedTypeSystem(TypeSystem first, TypeSystem second)
            : base(new ReferenceClassifierFactory())
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
                m_systemVoidStarTypeIndex = first.SystemVoidStarTypeIndex;
            }
            else if (second.SystemStringTypeIndex != -1)
            {
                m_systemStringTypeIndex = second.SystemStringTypeIndex;
                m_systemStringLengthOffset = second.SystemStringLengthOffset;
                m_systemStringFirstCharOffset = second.SystemStringFirstCharOffset;
                m_systemVoidStarTypeIndex = second.SystemVoidStarTypeIndex;
            }
            else
            {
                m_systemStringTypeIndex = -1;
                m_systemVoidStarTypeIndex = -1;
            }
        }

        public override int PointerSize => m_pointerSize;

        public override int NumberOfTypeIndices => m_first.NumberOfTypeIndices + m_second.NumberOfTypeIndices;

        public override string Assembly(int typeIndex)
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

        public override string QualifiedName(int typeIndex)
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

        public override string UnqualifiedName(int typeIndex)
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

        public override int BaseOrElementTypeIndex(int typeIndex)
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

        public override int ObjectHeaderSize(int typeIndex)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.ObjectHeaderSize(typeIndex);
            }
            else
            {
                return m_second.ObjectHeaderSize(typeIndex - m_first.NumberOfTypeIndices);
            }
        }

        public override int BaseSize(int typeIndex)
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

        public override bool IsValueType(int typeIndex)
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

        public override bool IsArray(int typeIndex)
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

        public override int Rank(int typeIndex)
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

        public override int NumberOfFields(int typeIndex)
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

        public override int FieldOffset(int typeIndex, int fieldNumber, bool withHeader)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.FieldOffset(typeIndex, fieldNumber, withHeader);
            }
            else
            {
                return m_second.FieldOffset(typeIndex - m_first.NumberOfTypeIndices, fieldNumber, withHeader);
            }
        }

        public override int FieldType(int typeIndex, int fieldNumber)
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

        public override string FieldName(int typeIndex, int fieldNumber)
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

        public override bool FieldIsStatic(int typeIndex, int fieldNumber)
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

        public override MemoryView StaticFieldBytes(int typeIndex, int fieldNumber)
        {
            if (typeIndex < m_first.NumberOfTypeIndices)
            {
                return m_first.StaticFieldBytes(typeIndex, fieldNumber);
            }
            else
            {
                return m_second.StaticFieldBytes(typeIndex - m_first.NumberOfTypeIndices, fieldNumber);
            }
        }

        public override int GetArrayElementOffset(int elementTypeIndex, int elementIndex)
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

        public override int GetArrayElementSize(int elementTypeIndex)
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

        public override int SystemStringTypeIndex => m_systemStringTypeIndex;

        public override int SystemStringLengthOffset => m_systemStringLengthOffset;

        public override int SystemStringFirstCharOffset => m_systemStringFirstCharOffset;

        public override int SystemVoidStarTypeIndex => m_systemVoidStarTypeIndex;

        public override IEnumerable<string> DumpStats()
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
