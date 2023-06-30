// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class TypeSet
    {
        readonly TypeSystem m_typeSystem;
        readonly HashSet<int> m_typeIndices;

        public TypeSet(TypeSystem typeSystem)
        {
            m_typeSystem = typeSystem;
            m_typeIndices = new HashSet<int>();
        }

        public int Count => m_typeIndices.Count;

        public bool Contains(int typeIndex)
        {
            return m_typeIndices.Contains(typeIndex);
        }

        public IEnumerable<int> TypeIndices => m_typeIndices;

        public void Add(int typeIndex)
        {
            m_typeIndices.Add(typeIndex);
        }

        public void AddTypesByName(string namePattern, string? assemblyOpt)
        {
            bool anchoredAtStart = namePattern.StartsWith(@"\<");
            bool anchoredAtEnd = namePattern.EndsWith(@"\>");

            for (int typeIndex = 0; typeIndex < m_typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                if (assemblyOpt != null && m_typeSystem.Assembly(typeIndex) != assemblyOpt)
                {
                    continue;
                }

                string qualifiedName = m_typeSystem.QualifiedName(typeIndex);

                bool matches;
                if (anchoredAtStart)
                {
                    if (anchoredAtEnd)
                    {
                        matches = qualifiedName.Equals(namePattern[2..^2], StringComparison.Ordinal);
                    }
                    else
                    {
                        matches = qualifiedName.StartsWith(namePattern[2..], StringComparison.Ordinal);
                    }
                }
                else if (anchoredAtEnd)
                {
                    matches = qualifiedName.EndsWith(namePattern[..^2], StringComparison.Ordinal);
                }
                else
                {
                    matches = qualifiedName.Contains(namePattern, StringComparison.Ordinal);
                }

                if (matches)
                {
                    m_typeIndices.Add(typeIndex);
                }
            }
        }

        public void AddDerivedTypes()
        {
            var negativeTypeIndices = new HashSet<int>();
            for (int typeIndex = 0; typeIndex < m_typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                _ = WalkHierarchy(typeIndex, negativeTypeIndices);
            }
        }

        bool WalkHierarchy(int typeIndex, HashSet<int> negativeTypeIndices)
        {
            if (m_typeIndices.Contains(typeIndex))
            {
                return true;
            }
            else if (negativeTypeIndices.Contains(typeIndex))
            {
                return false;
            }
            else if (m_typeSystem.IsArray(typeIndex))
            {
                // We don't have any scenarios yet that care about arrays, so we just return "false"
                // instead of making a decision on covariance vs contravariance here.
                return false;
            }

            int baseTypeIndex = m_typeSystem.BaseOrElementTypeIndex(typeIndex);
            if (baseTypeIndex == -1)
            {
                negativeTypeIndices.Add(typeIndex);
                return false;
            }

            bool isDerivedType = WalkHierarchy(baseTypeIndex, negativeTypeIndices);
            if (isDerivedType)
            {
                m_typeIndices.Add(typeIndex);
                return true;
            }
            else
            {
                negativeTypeIndices.Add(typeIndex);
                return false;
            }
        }
    }
}
