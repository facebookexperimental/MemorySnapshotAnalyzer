/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
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

        static ReadOnlySpan<char> AssemblyWithoutExtension(ReadOnlySpan<char> assemblyName)
        {
            if (assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return assemblyName[..^4];
            }
            else
            {
                return assemblyName;
            }
        }

        static bool AssemblyMatches(string assemblyName, ReadOnlySpan<char> assemblyWithoutExtension)
        {
            return assemblyWithoutExtension.Equals(AssemblyWithoutExtension(assemblyName.AsSpan()), StringComparison.OrdinalIgnoreCase);
        }

        // Note: can throw RegexParseException
        public void AddTypesByName(string input)
        {
            string namePattern;
            string? assemblyWithoutExtensionOpt;

            int indexOfColon = input.IndexOf(':');
            if (indexOfColon != -1)
            {
                assemblyWithoutExtensionOpt = new string(AssemblyWithoutExtension(input.AsSpan(0, indexOfColon)));
                namePattern = input[(indexOfColon + 1)..];
            }
            else
            {
                assemblyWithoutExtensionOpt = null;
                namePattern = input;
            }

            var regex = new Regex(namePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

            for (int typeIndex = 0; typeIndex < m_typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                if (assemblyWithoutExtensionOpt != null && !AssemblyMatches(m_typeSystem.Assembly(typeIndex), assemblyWithoutExtensionOpt))
                {
                    continue;
                }

                string qualifiedName = m_typeSystem.QualifiedName(typeIndex);
                if (regex.IsMatch(qualifiedName))
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
