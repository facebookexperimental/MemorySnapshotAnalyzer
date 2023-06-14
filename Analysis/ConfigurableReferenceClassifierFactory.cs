// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class ConfigurableReferenceClassifierFactory : ReferenceClassifierFactory
    {
        static ReadOnlySpan<char> WithoutExtension(string assemblyName)
        {
            ReadOnlySpan<char> assemblySpan = assemblyName.AsSpan();
            if (assemblySpan.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return assemblySpan.Slice(0, assemblySpan.Length - 4);
            }
            else
            {
                return assemblySpan;
            }
        }

        public struct ConfigurationEntry
        {
            // Can be a full assembly name (without .dll extension). Matched case-insensitively,
            // and assemblies that include a .dll extension are considered to match.
            public string Assembly;

            // Fully qualified class name.
            public string ClassName;

            // A field name, or "*" to indicate all fields.
            public string FieldPattern;

            public bool AssemblyMatches(ReadOnlySpan<char> assemblyWithoutExtension)
            {
                return assemblyWithoutExtension.Equals(WithoutExtension(Assembly), StringComparison.OrdinalIgnoreCase);
            }
        };

        public sealed class ConfigurableReferenceClassifier : ReferenceClassifier
        {
            readonly TypeSystem m_typeSystem;
            readonly HashSet<(int, int)> m_owningReferences;

            public ConfigurableReferenceClassifier(TypeSystem typeSystem, List<ConfigurationEntry> configurationEntries)
            {
                m_typeSystem = typeSystem;
                m_owningReferences = new HashSet<(int, int)>();

                // Create a lookup table that maps type indices to the field patterns that apply to that type.
                var assemblyToConfiguration = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
                for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
                {
                    Dictionary<string, List<string>> assemblyConfiguration = AssemblyConfiguration(typeIndex, assemblyToConfiguration, configurationEntries);

                    string className = typeSystem.QualifiedName(typeIndex);
                    if (assemblyConfiguration.TryGetValue(className, out List<string>? fieldPatterns)) {
                        ClassifyFields(typeIndex, fieldPatterns);
                    }
                }
            }

            Dictionary<string, List<string>> AssemblyConfiguration(
                int typeIndex,
                Dictionary<string, Dictionary<string, List<string>>> assemblyToConfiguration,
                List<ConfigurationEntry> configurationEntries)
            {
                string assemblyName = m_typeSystem.Assembly(typeIndex);
                if (!assemblyToConfiguration.TryGetValue(assemblyName, out Dictionary<string, List<string>>? assemblyConfiguration))
                {
                    // We have discovered a new assembly name. Filter the list of configuration entries that apply to the given assembly,
                    // and add them to the lookup structure.
                    ReadOnlySpan<char> typeAssembly = WithoutExtension(m_typeSystem.Assembly(typeIndex));

                    assemblyConfiguration = new Dictionary<string, List<string>>();
                    foreach (var configurationEntry in configurationEntries)
                    {
                        if (configurationEntry.AssemblyMatches(typeAssembly))
                        {
                            if (assemblyConfiguration.TryGetValue(configurationEntry.ClassName, out List<string>? configurationFieldPatterns))
                            {
                                configurationFieldPatterns!.Add(configurationEntry.FieldPattern);
                            }
                            else
                            {
                                assemblyConfiguration.Add(configurationEntry.ClassName, new List<string>() { configurationEntry.FieldPattern });
                            }
                        }
                    }

                    assemblyToConfiguration.Add(assemblyName, assemblyConfiguration);
                }

                return assemblyConfiguration;
            }

            void ClassifyFields(int typeIndex, List<string> fieldPatterns)
            {
                int numberOfFields = m_typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    string fieldName = m_typeSystem.FieldName(typeIndex, fieldNumber);
                    bool isOwningReference = false;
                    foreach (string fieldPattern in fieldPatterns)
                    {
                        if (fieldPattern.EndsWith("*", StringComparison.Ordinal))
                        {
                            isOwningReference = fieldName.AsSpan().Equals(fieldPattern.AsSpan()[..(fieldPattern.Length - 1)], StringComparison.Ordinal);
                        }
                        else
                        {
                            isOwningReference = fieldName.Equals(fieldPattern, StringComparison.Ordinal);
                        }

                        if (isOwningReference)
                        {
                            break;
                        }
                    }

                    if (isOwningReference)
                    {
                        m_owningReferences.Add((typeIndex, fieldNumber));
                    }
                }
            }

            public override bool IsOwningReference(int typeIndex, int fieldNumber)
            {
                return m_owningReferences.Contains((typeIndex, fieldNumber));
            }
        }

        readonly List<ConfigurationEntry> m_configurationEntries;

        public ConfigurableReferenceClassifierFactory(List<ConfigurationEntry> configurationEntries)
        {
            m_configurationEntries = configurationEntries;
        }

        public override ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new ConfigurableReferenceClassifier(typeSystem, m_configurationEntries);
        }
    }
}
