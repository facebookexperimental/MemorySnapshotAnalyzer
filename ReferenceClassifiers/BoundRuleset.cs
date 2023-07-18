// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    sealed class BoundRuleset
    {
        readonly TypeSystem m_typeSystem;
        readonly Dictionary<(int typeIndex, int fieldNumber), PointerFlags> m_specialReferences;
        readonly Dictionary<(int typeIndex, int fieldNumber), List<(int typeIndex, int fieldNumber)[]>> m_conditionAnchors;

        internal BoundRuleset(TypeSystem typeSystem, List<Rule> rules)
        {
            m_typeSystem = typeSystem;
            m_specialReferences = new();
            m_conditionAnchors = new();

            List<(TypeSpec spec, string fieldPattern, (int ruleNumber, PointerFlags pointerFlags))> specs = new();
            for (int ruleNumber = 0; ruleNumber < rules.Count; ruleNumber++)
            {
                if (rules[ruleNumber] is OwnsFieldPatternRule fieldPatternRule)
                {
                    specs.Add((fieldPatternRule.TypeSpec, fieldPatternRule.FieldPattern, (ruleNumber, PointerFlags.IsOwningReference)));
                }
                else if (rules[ruleNumber] is OwnsFieldPathRule fieldPathRule)
                {
                    specs.Add((fieldPathRule.TypeSpec, fieldPathRule.Selector[0], (ruleNumber, PointerFlags.IsConditionAnchor)));
                }
                else if (rules[ruleNumber] is WeakRule weakRule)
                {
                    specs.Add((weakRule.TypeSpec, weakRule.FieldPattern, (ruleNumber, PointerFlags.IsWeakReference)));
                }
                else if (rules[ruleNumber] is ExternalRule externalRule)
                {
                    specs.Add((externalRule.TypeSpec, externalRule.FieldPattern, (ruleNumber, PointerFlags.IsExternalReference)));
                }
            }

            var matcher = new Matcher<(int ruleNumber, PointerFlags pointerFlags)>(m_typeSystem, specs);

            var processField = (int typeIndex, int fieldNumber, (int ruleNumber, PointerFlags pointerFlags) data) =>
            {
                m_specialReferences.Add((typeIndex, fieldNumber), data.pointerFlags);

                if (rules[data.ruleNumber] is OwnsFieldPathRule ownsFieldPathRule)
                {
                    if (!m_conditionAnchors.TryGetValue((typeIndex, fieldNumber), out List<(int typeIndex, int fieldNumber)[]>? fieldPaths))
                    {
                        fieldPaths = new();
                        m_conditionAnchors.Add((typeIndex, fieldNumber), fieldPaths);
                    }

                    var fieldPath = FindFieldPath(typeIndex, ownsFieldPathRule.Selector);
                    if (fieldPath != null)
                    {
                        fieldPaths.Add(fieldPath);
                    }
                }
            };

            for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                matcher.ForAllMatchingFields(typeIndex, processField);
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

        internal PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
        {
            if (m_specialReferences.TryGetValue((typeIndex, fieldNumber), out PointerFlags pointerFlags))
            {
                return pointerFlags;
            }
            else
            {
                return PointerFlags.None;
            }
        }

        internal List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
        {
            return m_conditionAnchors[(typeIndex, fieldNumber)];
        }
    }
}
