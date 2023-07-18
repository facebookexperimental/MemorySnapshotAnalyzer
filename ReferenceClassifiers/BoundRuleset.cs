// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    sealed class BoundRuleset
    {
        readonly TypeSystem m_typeSystem;
        readonly HashSet<(int typeIndex, int fieldNumber)> m_owningReferences;
        readonly Dictionary<(int typeIndex, int fieldNumber), List<(int typeIndex, int fieldNumber)[]>> m_conditionAnchors;
        readonly HashSet<(int typeIndex, int fieldNumber)> m_weakReferences;

        internal BoundRuleset(TypeSystem typeSystem, List<Rule> rules)
        {
            m_typeSystem = typeSystem;
            m_owningReferences = new();
            m_conditionAnchors = new();
            m_weakReferences = new();

            var shallowSpecs = new List<(TypeSpec spec, string fieldPattern, int ruleNumber)>();
            var deepSpecs = new List<(TypeSpec spec, string fieldName, int ruleNumber)>();
            var weakSpecs = new List<(TypeSpec spec, string fieldName, int ruleNumber)>();
            for (int ruleNumber = 0; ruleNumber < rules.Count; ruleNumber++)
            {
                if (rules[ruleNumber] is OwnsFieldPatternRule fieldPatternRule)
                {
                    shallowSpecs.Add((fieldPatternRule.TypeSpec, fieldPatternRule.FieldPattern, ruleNumber));
                }
                else if (rules[ruleNumber] is OwnsFieldPathRule fieldPathRule)
                {
                    deepSpecs.Add((fieldPathRule.TypeSpec, fieldPathRule.Selector[0], ruleNumber));
                }
                else if (rules[ruleNumber] is WeakRule weakRule)
                {
                    weakSpecs.Add((weakRule.TypeSpec, weakRule.FieldPattern, ruleNumber));
                }
            }

            var owningReferenceMatcher = new Matcher(m_typeSystem, shallowSpecs);
            var anchorMatcher = new Matcher(m_typeSystem, deepSpecs);
            var weakMatcher = new Matcher(m_typeSystem, weakSpecs);

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
                            var fieldPathRule = (OwnsFieldPathRule)rules[ruleNumber];
                            var fieldPath = FindFieldPath(typeIndex, fieldPathRule.Selector!);
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

                var weakFieldPatterns = weakMatcher.GetFieldPatterns(typeIndex);
                if (weakFieldPatterns != null)
                {
                    ClassifyFields(typeIndex, weakFieldPatterns,
                        (fieldNumber, ruleNumber) => m_weakReferences.Add((typeIndex, fieldNumber)));
                }
            }
        }

        static readonly int s_fieldIsArraySentinel = Int32.MaxValue;

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

        internal PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
        {
            PointerFlags pointerFlags = PointerFlags.None;
            if (m_owningReferences.Contains((typeIndex, fieldNumber)))
            {
                pointerFlags |= PointerFlags.IsOwningReference;
            }

            if (m_conditionAnchors.ContainsKey((typeIndex, fieldNumber)))
            {
                pointerFlags |= PointerFlags.IsConditionAnchor;
            }

            if (m_weakReferences.Contains((typeIndex, fieldNumber)))
            {
                pointerFlags |= PointerFlags.IsWeakReference;
            }

            return pointerFlags;
        }

        internal List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
        {
            return m_conditionAnchors[(typeIndex, fieldNumber)];
        }
    }
}
