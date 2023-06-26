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

        public abstract class Rule
        {
            public ClassSpec Spec;
        };

        public sealed class FieldPatternRule : Rule
        {
            // A field name, or (if ending in "*") a field prefix.
            public string? FieldPattern;
        }

        public sealed class FieldPathRule : Rule
        {
            // Path of fields to dereference. Note that these are full field names, not patterns.
            // The special field name "[]" represents array indexing (covering all elements of the array).
            public string[]? FieldNames;
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
            readonly HashSet<(int typeIndex, int fieldNumber)> m_owningReferences;
            readonly Dictionary<(int typeIndex, int fieldNumber), List<(int typeIndex, int fieldNumber)[]>> m_conditionAnchors;

            public ConfigurableReferenceClassifier(TypeSystem typeSystem, List<Rule> rules)
            {
                m_typeSystem = typeSystem;
                m_owningReferences = new HashSet<(int typeIndex, int fieldNumber)>();
                m_conditionAnchors = new Dictionary<(int typeIndex, int fieldNumber), List<(int typeIndex, int fieldNumber)[]>>();

                var shallowSpecs = new List<(ClassSpec spec, string fieldPattern, int ruleNumber)>();
                var deepSpecs = new List<(ClassSpec spec, string fieldName, int ruleNumber)>();
                for (int ruleNumber = 0; ruleNumber < rules.Count; ruleNumber++)
                {
                    if (rules[ruleNumber] is FieldPatternRule fieldPatternRule)
                    {
                        shallowSpecs.Add((rules[ruleNumber].Spec, fieldPatternRule.FieldPattern!, ruleNumber));
                    }
                    else
                    {
                        var fieldPathRule = (FieldPathRule)rules[ruleNumber];
                        deepSpecs.Add((rules[ruleNumber].Spec, fieldPathRule.FieldNames![0], ruleNumber));
                    }
                }

                var owningReferenceMatcher = new Matcher(m_typeSystem, shallowSpecs);
                var anchorMatcher = new Matcher(m_typeSystem, deepSpecs);

                for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
                {
                    var owningReferenceFieldPatterns = owningReferenceMatcher.GetFieldPatterns(typeIndex);
                    if (owningReferenceFieldPatterns != null)
                    {
                        ClassifyFields(typeIndex, owningReferenceFieldPatterns,
                            (fieldNumber, ruleNumber) => m_owningReferences.Add((typeIndex, fieldNumber)));
                    }

                    var anchorFieldPatterns = anchorMatcher.GetFieldPatterns(typeIndex);
                    if (anchorFieldPatterns != null)
                    {
                        ClassifyFields(typeIndex, anchorFieldPatterns,
                            (fieldNumber, ruleNumber) =>
                            {
                                var fieldPathRule = (FieldPathRule)rules[ruleNumber];
                                var fieldPath = FindFieldPath(typeIndex, fieldPathRule.FieldNames!);
                                if (fieldPath != null)
                                {
                                    if (!m_conditionAnchors.TryGetValue((typeIndex, fieldNumber), out List<(int typeIndex, int fieldNumber)[]>? fieldPaths))
                                    {
                                        fieldPaths = new List<(int typeIndex, int fieldNumber)[]>();
                                        m_conditionAnchors.Add((typeIndex, fieldNumber), fieldPaths);
                                    }

                                    fieldPaths.Add(fieldPath);
                                }
                            });
                    }
                }
            }

            static int s_fieldIsArraySentinel = Int32.MaxValue;

            (int typeIndex, int fieldNumber)[]? FindFieldPath(int typeIndex, string[] fieldNames)
            {
                var fieldPath = new (int typeIndex, int fieldNumber)[fieldNames.Length];
                int currentTypeIndex = typeIndex;
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    int fieldNumber;
                    int fieldTypeIndex;
                    if (fieldNames[i] == "[]")
                    {
                        // Sentinel value for array indexing (all elements)
                        fieldNumber = s_fieldIsArraySentinel;
                        if (!m_typeSystem.IsArray(currentTypeIndex))
                        {
                            // TODO: better warning management
                            Console.Error.WriteLine($"field was expected to be an array type; found {m_typeSystem.QualifiedName(currentTypeIndex)}");
                            return null;
                        }

                        fieldTypeIndex = m_typeSystem.BaseOrElementTypeIndex(currentTypeIndex);
                    }
                    else
                    {
                        fieldNumber = m_typeSystem.GetFieldNumber(currentTypeIndex, fieldNames[i]);
                        if (fieldNumber == -1)
                        {
                            // TODO: better warning management
                            Console.Error.WriteLine($"field {fieldNames[i]} not found in type {m_typeSystem.QualifiedName(currentTypeIndex)}");
                            return null;
                        }

                        fieldTypeIndex = m_typeSystem.FieldType(currentTypeIndex, fieldNumber);
                    }

                    fieldPath[i] = (currentTypeIndex, fieldNumber);
                    currentTypeIndex = fieldTypeIndex;
                }

                if (m_typeSystem.IsValueType(currentTypeIndex))
                {
                    // TODO: better warning management
                    Console.Error.WriteLine("field path for {0}.{1} ending in non-reference type field {2} of type {3}",
                        m_typeSystem.QualifiedName(typeIndex),
                        fieldNames[0],
                        fieldNames[^1],
                        m_typeSystem.QualifiedName(currentTypeIndex));
                    return null;
                }

                return fieldPath;
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
                return m_owningReferences.Contains((typeIndex, fieldNumber));
            }

            public override bool IsConditionAnchor(int typeIndex, int fieldNumber)
            {
                return m_conditionAnchors.ContainsKey((typeIndex, fieldNumber));
            }

            public override List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
            {
                return m_conditionAnchors[(typeIndex, fieldNumber)];
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
