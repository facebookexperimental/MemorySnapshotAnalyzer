// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    // Creates a lookup table that maps type indices to the field patterns that apply to that type.
    sealed class Matcher
    {
        readonly TypeSystem m_typeSystem;
        readonly List<(TypeSpec spec, string fieldPattern, int ruleNumber)> m_specs;
        readonly Dictionary<string, Dictionary<string, List<(string fieldPattern, int ruleNumber)>>> m_assemblyToConfiguration;

        internal Matcher(TypeSystem typeSystem, List<(TypeSpec spec, string fieldPattern, int ruleNumber)> specs)
        {
            m_typeSystem = typeSystem;
            m_specs = specs;
            m_assemblyToConfiguration = new Dictionary<string, Dictionary<string, List<(string fieldPattern, int ruleNumber)>>>(StringComparer.OrdinalIgnoreCase);
        }

        internal List<(string fieldPattern, int ruleNumber)>? GetFieldPatterns(int typeIndex)
        {
            Dictionary<string, List<(string fieldPattern, int ruleNumber)>> assemblyConfiguration = AssemblyConfiguration(typeIndex);

            string className = m_typeSystem.QualifiedName(typeIndex);
            if (assemblyConfiguration.TryGetValue(className, out List<(string fieldPattern, int ruleNumber)>? fieldPatterns))
            {
                return fieldPatterns;
            }
            else
            {
                return null;
            }
        }

        internal static int TestFieldName(string fieldName, List<(string fieldPattern, int ruleNumber)> fieldPatterns)
        {
            foreach ((string fieldPattern, int ruleNumber) in fieldPatterns)
            {
                if (fieldPattern.EndsWith("*", StringComparison.Ordinal))
                {
                    if (fieldName.AsSpan().StartsWith(fieldPattern.AsSpan()[..^1], StringComparison.Ordinal))
                    {
                        return ruleNumber;
                    }
                }
                else
                {
                    if (fieldName.Equals(fieldPattern, StringComparison.Ordinal))
                    {
                        return ruleNumber;
                    }
                }
            }

            return -1;
        }

        Dictionary<string, List<(string fieldPattern, int ruleNumber)>> AssemblyConfiguration(int typeIndex)
        {
            string assemblyName = m_typeSystem.Assembly(typeIndex);
            if (!m_assemblyToConfiguration.TryGetValue(assemblyName, out Dictionary<string, List<(string fieldPattern, int ruleNumber)>>? assemblyConfiguration))
            {
                // We have discovered a new assembly name. Filter the list of configuration entries that apply to the given assembly,
                // and add them to the lookup structure.
                ReadOnlySpan<char> typeAssembly = TypeSpec.WithoutExtension(m_typeSystem.Assembly(typeIndex));

                assemblyConfiguration = new Dictionary<string, List<(string fieldPattern, int ruleNumber)>>();
                foreach ((TypeSpec spec, string fieldPattern, int ruleNumber) in m_specs)
                {
                    if (spec.AssemblyMatches(typeAssembly))
                    {
                        if (assemblyConfiguration.TryGetValue(spec.TypeName, out List<(string fieldPattern, int ruleNumber)>? configurationFieldPatterns))
                        {
                            configurationFieldPatterns!.Add((fieldPattern, ruleNumber));
                        }
                        else
                        {
                            assemblyConfiguration.Add(spec.TypeName, new List<(string fieldPattern, int ruleNumber)>() { (fieldPattern, ruleNumber) });
                        }
                    }
                }

                m_assemblyToConfiguration.Add(assemblyName, assemblyConfiguration);
            }

            return assemblyConfiguration;
        }
    }
}
