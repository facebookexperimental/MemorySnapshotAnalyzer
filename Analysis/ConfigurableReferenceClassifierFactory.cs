// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.Analysis
{
    public sealed class ConfigurableReferenceClassifierFactory : ReferenceClassifierFactory
    {
        public struct ClassSpec
        {
            // Can be a full assembly name (without .dll extension). Matched case-insensitively,
            // and assemblies that include a .dll extension are considered to match.
            public string Assembly;

            // Fully qualified class name.
            public string ClassName;

            public bool AssemblyMatches(ReadOnlySpan<char> assemblyWithoutExtension)
            {
                return assemblyWithoutExtension.Equals(WithoutExtension(Assembly), StringComparison.OrdinalIgnoreCase);
            }

            public static ReadOnlySpan<char> WithoutExtension(string assemblyName)
            {
                ReadOnlySpan<char> assemblySpan = assemblyName.AsSpan();
                if (assemblySpan.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    return assemblySpan[..^4];
                }
                else
                {
                    return assemblySpan;
                }
            }
        }

        public class Rule
        {
            public ClassSpec Spec;

            // A field name, or "*" to indicate all fields.
            public string? FieldPattern;
        };

        public sealed class AncestorRule : Rule
        {
            public int UpwardLevels;

            public ClassSpec AncestorClassSpec;

            public string? AncestorField;
        }

        // Creates a lookup table that maps type indices to the field patterns that apply to that type.
        sealed class Matcher
        {
            readonly TypeSystem m_typeSystem;
            readonly List<(ClassSpec spec, string fieldPattern, int ruleNumber)> m_specs;
            readonly Dictionary<string, Dictionary<string, List<(string fieldPattern, int ruleNumber)>>> m_assemblyToConfiguration;

            internal Matcher(TypeSystem typeSystem, List<(ClassSpec spec, string fieldPattern, int ruleNumber)> specs)
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
                    ReadOnlySpan<char> typeAssembly = ClassSpec.WithoutExtension(m_typeSystem.Assembly(typeIndex));

                    assemblyConfiguration = new Dictionary<string, List<(string fieldPattern, int ruleNumber)>>();
                    foreach ((ClassSpec spec, string fieldPattern, int ruleNumber) in m_specs)
                    {
                        if (spec.AssemblyMatches(typeAssembly))
                        {
                            if (assemblyConfiguration.TryGetValue(spec.ClassName, out List<(string fieldPattern, int ruleNumber)>? configurationFieldPatterns))
                            {
                                configurationFieldPatterns!.Add((fieldPattern, ruleNumber));
                            }
                            else
                            {
                                assemblyConfiguration.Add(spec.ClassName, new List<(string fieldPattern, int ruleNumber)>() { (fieldPattern, ruleNumber) });
                            }
                        }
                    }

                    m_assemblyToConfiguration.Add(assemblyName, assemblyConfiguration);
                }

                return assemblyConfiguration;
            }
        }

        public sealed class ConfigurableReferenceClassifier : ReferenceClassifier
        {
            readonly TypeSystem m_typeSystem;
            readonly HashSet<(int, int)> m_unconditionalOwningReferences;
            readonly Dictionary<(int, int), (int, Dictionary<int, List<int>>)> m_conditionalOwningReferences;

            public ConfigurableReferenceClassifier(TypeSystem typeSystem, List<Rule> rules)
            {
                m_typeSystem = typeSystem;
                m_unconditionalOwningReferences = new HashSet<(int, int)>();
                m_conditionalOwningReferences = new Dictionary<(int, int), (int, Dictionary<int, List<int>>)> ();

                var unconditionalSpecs = new List<(ClassSpec spec, string fieldPattern, int ruleNumber)>();
                var conditionalSpecs = new List<(ClassSpec spec, string fieldPattern, int ruleNumber)>();
                var conditionMatchers = new Dictionary<int, Matcher>();
                var conditionRuleToTypeToFields = new Dictionary<int, Dictionary<int, List<int>>>();
                for (int ruleNumber = 0; ruleNumber < rules.Count; ruleNumber++)
                {
                    if (rules[ruleNumber] is AncestorRule ancestorRule)
                    {
                        conditionalSpecs.Add((rules[ruleNumber].Spec, rules[ruleNumber].FieldPattern!, ruleNumber));
                        var conditionSpec = ancestorRule.AncestorClassSpec;
                        conditionMatchers.Add(ruleNumber, new Matcher(m_typeSystem,
                            new List<(ClassSpec spec, string fieldPattern, int ruleNumber)>() { (conditionSpec, ancestorRule.FieldPattern!, ruleNumber) }));
                        conditionRuleToTypeToFields.Add(ruleNumber, new Dictionary<int, List<int>>());
                    }
                    else
                    {
                        unconditionalSpecs.Add((rules[ruleNumber].Spec, rules[ruleNumber].FieldPattern!, ruleNumber));
                    }
                }

                var unconditionalMatcher = new Matcher(m_typeSystem, unconditionalSpecs);
                var conditionalMatcher = new Matcher(m_typeSystem, conditionalSpecs);

                for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
                {
                    var unconditionalFieldPatterns = unconditionalMatcher.GetFieldPatterns(typeIndex);
                    if (unconditionalFieldPatterns != null)
                    {
                        ClassifyFields(typeIndex, unconditionalFieldPatterns,
                            (fieldNumber, ruleNumber) => m_unconditionalOwningReferences.Add((typeIndex, fieldNumber)));
                    }

                    var conditionalFieldPatterns = conditionalMatcher.GetFieldPatterns(typeIndex);
                    if (conditionalFieldPatterns != null)
                    {
                        ClassifyFields(typeIndex, conditionalFieldPatterns,
                            (fieldNumber, ruleNumber) =>
                            {
                                var ancestorRule = (AncestorRule)rules[ruleNumber];
                                m_conditionalOwningReferences.Add((typeIndex, fieldNumber),
                                    (ancestorRule.UpwardLevels, conditionRuleToTypeToFields[ruleNumber]));
                            });
                    }

                    foreach (var kvp in conditionMatchers)
                    {
                        var conditionFieldPatterns = kvp.Value.GetFieldPatterns(typeIndex);
                        if (conditionFieldPatterns != null)
                        {
                            Dictionary<int, List<int>> typeToFields = conditionRuleToTypeToFields[kvp.Key];
                            ClassifyFields(typeIndex, conditionFieldPatterns,
                                (fieldNumber, _) =>
                                {
                                    if (typeToFields.TryGetValue(typeIndex, out List<int>? fieldNumbers))
                                    {
                                        fieldNumbers!.Add(fieldNumber);
                                    }
                                    else
                                    {
                                        typeToFields.Add(typeIndex, new List<int>() { fieldNumber });
                                    }
                                });
                        }
                    }
                }
            }

            void ClassifyFields(int typeIndex, List<(string fieldPattern, int ruleNumber)> fieldPatterns, Action<int, int> add)
            {
                int numberOfFields = m_typeSystem.NumberOfFields(typeIndex);
                for (int fieldNumber = 0; fieldNumber < numberOfFields; fieldNumber++)
                {
                    string fieldName = m_typeSystem.FieldName(typeIndex, fieldNumber);
                    int ruleNumber = Matcher.TestFieldName(fieldName, fieldPatterns);
                    if (ruleNumber != -1)
                    {
                        add(fieldNumber, ruleNumber);
                    }
                }
            }

            public override bool IsOwningReference(int typeIndex, int fieldNumber)
            {
                return m_unconditionalOwningReferences.Contains((typeIndex, fieldNumber));
            }

            public override bool IsConditionalOwningReference(int typeIndex, int fieldNumber)
            {
                return m_conditionalOwningReferences.ContainsKey((typeIndex, fieldNumber));
            }

            public override bool CheckConditionalOwningReference(int typeIndex, int fieldNumber, Func<int, int> getAncestorTypeIndex, Func<int, bool> testField)
            {
                if (m_conditionalOwningReferences.TryGetValue((typeIndex, fieldNumber), out (int UpwardLevels, Dictionary<int, List<int>> TypeToFields) pair))
                {
                    int ancestorTypeIndex = getAncestorTypeIndex(pair.UpwardLevels);
                    if (pair.TypeToFields.TryGetValue(ancestorTypeIndex, out List<int>? ancestorFieldNumbers))
                    {
                        foreach (int ancestorFieldNumber in ancestorFieldNumbers!)
                        {
                            if (testField(ancestorFieldNumber))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
        }

        readonly string m_description;
        readonly List<Rule> m_configurationEntries;

        public ConfigurableReferenceClassifierFactory(string description, List<Rule> configurationEntries)
        {
            m_description = description;
            m_configurationEntries = configurationEntries;
        }

        public override ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new ConfigurableReferenceClassifier(typeSystem, m_configurationEntries);
        }

        public override string Description => m_description;
    }
}
