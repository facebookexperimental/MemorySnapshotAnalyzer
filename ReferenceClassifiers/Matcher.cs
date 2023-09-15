/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    // Creates a lookup table that maps type indices to the field patterns that apply to that type.
    sealed class Matcher<T>
    {
        readonly TypeSystem m_typeSystem;
        readonly List<(TypeSpec typeSpec, string fieldPattern, T data)> m_specs;
        readonly List<(Regex regex, List<(string fieldPattern, T data)>)> m_regexSpecs;
        readonly Dictionary<string, Dictionary<string, List<(string fieldPattern, T data)>>> m_assemblyToConfiguration;

        internal Matcher(TypeSystem typeSystem, List<(TypeSpec typeSpec, string fieldPattern, T data)> specs)
        {
            m_typeSystem = typeSystem;
            m_specs = new();
            m_regexSpecs = new();
            foreach ((TypeSpec typeSpec, string fieldPattern, T data) spec in specs)
            {
                if (spec.typeSpec.IsRegex)
                {
                    Regex regex = new(spec.typeSpec.TypeName, RegexOptions.Compiled | RegexOptions.CultureInvariant);
                    m_regexSpecs.Add((regex, new() { (spec.fieldPattern, spec.data) }));
                }
                else
                {
                    m_specs.Add(spec);
                }
            }
            m_assemblyToConfiguration = new(StringComparer.OrdinalIgnoreCase);
        }

        internal void ForAllMatchingFields(int typeIndex, Action<int, int, T> processField)
        {
            Dictionary<string, List<(string fieldPattern, T data)>> assemblyConfiguration = AssemblyConfiguration(typeIndex);
            List<(string fieldPattern, T data)>? fieldPatterns;
            if (assemblyConfiguration.TryGetValue(m_typeSystem.QualifiedName(typeIndex), out fieldPatterns))
            {
                ForAllMatchingFieldsHelper(typeIndex, fieldPatterns, processField);
            }
            else if (assemblyConfiguration.TryGetValue(m_typeSystem.QualifiedGenericNameWithArity(typeIndex), out fieldPatterns))
            {
                ForAllMatchingFieldsHelper(typeIndex, fieldPatterns, processField);
            }

            string qualifiedName = m_typeSystem.QualifiedName(typeIndex);
            foreach ((Regex regex, List<(string fieldPattern, T data)> regexFieldPatterns) in m_regexSpecs)
            {
                if (regex.IsMatch(qualifiedName))
                {
                    ForAllMatchingFieldsHelper(typeIndex, regexFieldPatterns, processField);
                }
            }
        }

        void ForAllMatchingFieldsHelper(int typeIndex, List<(string fieldPattern, T data)> fieldPatterns, Action<int, int, T> processField)
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

        Dictionary<string, List<(string fieldPattern, T data)>> AssemblyConfiguration(int typeIndex)
        {
            string assemblyName = m_typeSystem.Assembly(typeIndex);
            if (!m_assemblyToConfiguration.TryGetValue(assemblyName, out Dictionary<string, List<(string fieldPattern, T data)>>? assemblyConfiguration))
            {
                // We have discovered a new assembly name. Filter the list of configuration entries that apply to the given assembly,
                // and add them to the lookup structure.
                ReadOnlySpan<char> typeAssembly = TypeSpec.WithoutExtension(m_typeSystem.Assembly(typeIndex));

                assemblyConfiguration = new Dictionary<string, List<(string fieldPattern, T data)>>();
                foreach ((TypeSpec typeSpec, string fieldPattern, T data) in m_specs)
                {
                    if (typeSpec.AssemblyMatches(typeAssembly))
                    {
                        if (assemblyConfiguration.TryGetValue(typeSpec.TypeName, out List<(string fieldPattern, T data)>? configurationFieldPatterns))
                        {
                            configurationFieldPatterns.Add((fieldPattern, data));
                        }
                        else
                        {
                            assemblyConfiguration.Add(typeSpec.TypeName, new List<(string fieldPattern, T data)>() { (fieldPattern, data) });
                        }
                    }
                }

                m_assemblyToConfiguration.Add(assemblyName, assemblyConfiguration);
            }

            return assemblyConfiguration;
        }
    }
}
