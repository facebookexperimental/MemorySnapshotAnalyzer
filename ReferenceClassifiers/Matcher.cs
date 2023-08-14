/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    // Creates a lookup table that maps type indices to the field patterns that apply to that type.
    sealed class Matcher<T>
    {
        readonly TypeSystem m_typeSystem;
        readonly List<(TypeSpec spec, string fieldPattern, T data)> m_specs;
        readonly Dictionary<string, Dictionary<string, List<(string fieldPattern, T data)>>> m_assemblyToConfiguration;

        internal Matcher(TypeSystem typeSystem, List<(TypeSpec spec, string fieldPattern, T data)> specs)
        {
            m_typeSystem = typeSystem;
            m_specs = specs;
            m_assemblyToConfiguration = new(StringComparer.OrdinalIgnoreCase);
        }

        internal void ForAllMatchingFields(int typeIndex, Action<int, int, T> processField)
        {
            Dictionary<string, List<(string fieldPattern, T data)>> assemblyConfiguration = AssemblyConfiguration(typeIndex);

            string typeName = m_typeSystem.QualifiedName(typeIndex);
            if (assemblyConfiguration.TryGetValue(typeName, out List<(string fieldPattern, T data)>? fieldPatterns))
            {
                int numberOfFields = m_typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    string fieldName = m_typeSystem.FieldName(typeIndex, fieldNumber);
                    foreach ((string fieldPattern, T patternData) in fieldPatterns)
                    {
                        if (fieldPattern.EndsWith("*", StringComparison.Ordinal))
                        {
                            if (fieldName.AsSpan().StartsWith(fieldPattern.AsSpan()[..^1], StringComparison.Ordinal))
                            {
                                processField(typeIndex, fieldNumber, patternData);
                            }
                        }
                        else
                        {
                            if (fieldName.Equals(fieldPattern, StringComparison.Ordinal))
                            {
                                processField(typeIndex, fieldNumber, patternData);
                            }
                        }
                    }
                }
            }
        }

        Dictionary<string, List<(string fieldPattern, T data)>> AssemblyConfiguration(int typeIndex)
        {
            string assemblyName = m_typeSystem.Assembly(typeIndex);
            if (!m_assemblyToConfiguration.TryGetValue(assemblyName, out Dictionary<string, List<(string fieldPattern, T data)>>? assemblyConfiguration))
            {
                // We have discovered a new assembly name. Filter the list of configuration entries that apply to the given assembly,
                // and add them to the lookup structure.
                ReadOnlySpan<char> typeAssembly = TypeSpec.WithoutExtension(m_typeSystem.Assembly(typeIndex));

                assemblyConfiguration = new Dictionary<string, List<(string fieldPattern, T data)>>();
                foreach ((TypeSpec spec, string fieldPattern, T data) in m_specs)
                {
                    if (spec.AssemblyMatches(typeAssembly))
                    {
                        if (assemblyConfiguration.TryGetValue(spec.TypeName, out List<(string fieldPattern, T data)>? configurationFieldPatterns))
                        {
                            configurationFieldPatterns!.Add((fieldPattern, data));
                        }
                        else
                        {
                            assemblyConfiguration.Add(spec.TypeName, new List<(string fieldPattern, T data)>() { (fieldPattern, data) });
                        }
                    }
                }

                m_assemblyToConfiguration.Add(assemblyName, assemblyConfiguration);
            }

            return assemblyConfiguration;
        }
    }
}
