/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    sealed class BoundRuleset
    {
        static readonly string LOG_SOURCE = "ReferenceClassifiers";

        readonly TypeSystem m_typeSystem;
        readonly ILogger m_logger;
        readonly Dictionary<(int typeIndex, int fieldNumber), PointerFlags> m_specialReferences;
        readonly Dictionary<(int typeIndex, int fieldNumber), List<Selector>> m_conditionAnchors;
        readonly Dictionary<(int typeIndex, int fieldNumber), List<(Selector selector, List<string> tag)>> m_tagAnchors;
        readonly Dictionary<(int typeIndex, int fieldNumber), (List<string> zeroTags, List<string> nonZeroTags)> m_tags;

        internal BoundRuleset(TypeSystem typeSystem, List<Rule> rules, ILogger logger)
        {
            m_typeSystem = typeSystem;
            m_logger = logger;
            m_specialReferences = new();
            m_conditionAnchors = new();
            m_tagAnchors = new();
            m_tags = new();

            m_logger.Clear(LOG_SOURCE);

            List<(TypeSpec spec, string fieldPattern, (int ruleNumber, PointerFlags pointerFlags))> specs = new();
            for (int ruleNumber = 0; ruleNumber < rules.Count; ruleNumber++)
            {
                if (rules[ruleNumber] is OwnsRule ownsRule)
                {
                    PointerFlags pointerFlags = ownsRule.Selector.Length == 1 ? PointerFlags.IsOwningReference : PointerFlags.IsConditionAnchor;
                    specs.Add((ownsRule.TypeSpec, ownsRule.Selector[0], (ruleNumber, pointerFlags)));
                }
                else if (rules[ruleNumber] is WeakRule weakRule)
                {
                    specs.Add((weakRule.TypeSpec, weakRule.FieldPattern, (ruleNumber, PointerFlags.IsWeakReference)));
                }
                else if (rules[ruleNumber] is ExternalRule externalRule)
                {
                    specs.Add((externalRule.TypeSpec, externalRule.FieldPattern, (ruleNumber, PointerFlags.IsExternalReference)));
                }
                else if (rules[ruleNumber] is TagSelectorRule tagSelectorRule)
                {
                    specs.Add((tagSelectorRule.TypeSpec, tagSelectorRule.Selector[0], (ruleNumber, PointerFlags.IsTagAnchor)));
                }
                else if (rules[ruleNumber] is TagConditionRule tagConditionRule)
                {
                    PointerFlags pointerFlags = tagConditionRule.TagIfNonZero ? PointerFlags.TagIfNonZero : PointerFlags.TagIfZero;
                    specs.Add((tagConditionRule.TypeSpec, tagConditionRule.FieldPattern, (ruleNumber, pointerFlags)));
                }
            }

            var matcher = new Matcher<(int ruleNumber, PointerFlags pointerFlags)>(m_typeSystem, specs);

            var processField = (int typeIndex, int fieldNumber, (int ruleNumber, PointerFlags pointerFlags) data) =>
            {
                _ = m_specialReferences.TryGetValue((typeIndex, fieldNumber), out PointerFlags existingPointerFlags);
                PointerFlags newPointerFlags = existingPointerFlags | data.pointerFlags;
                if ((newPointerFlags & PointerFlags.IsExternalReference) != 0)
                {
                    newPointerFlags &= ~PointerFlags.Untraced;
                }
                else if (m_typeSystem.IsValueType(m_typeSystem.FieldType(typeIndex, fieldNumber)))
                {
                    newPointerFlags |= PointerFlags.Untraced;
                }

                m_specialReferences[(typeIndex, fieldNumber)] = newPointerFlags;

                if (rules[data.ruleNumber] is OwnsRule ownsRule && ownsRule.Selector.Length > 1)
                {
                    if (!m_conditionAnchors.TryGetValue((typeIndex, fieldNumber), out List<Selector>? selectors))
                    {
                        selectors = new();
                        m_conditionAnchors.Add((typeIndex, fieldNumber), selectors);
                    }

                    var selector = BindSelector(ownsRule, typeIndex, ownsRule.Selector, isDynamic: ownsRule.IsDynamic);
                    if (selector.StaticPrefix != null)
                    {
                        selectors.Add(selector);
                    }
                }
                else if (rules[data.ruleNumber] is TagSelectorRule tagSelectorRule)
                {
                    if (!m_tagAnchors.TryGetValue((typeIndex, fieldNumber), out List<(Selector selector, List<string> tags)>? selectors))
                    {
                        selectors = new();
                        m_tagAnchors.Add((typeIndex, fieldNumber), selectors);
                    }

                    var selector = BindSelector(tagSelectorRule, typeIndex, tagSelectorRule.Selector, tagSelectorRule.IsDynamic);
                    if (selector.StaticPrefix != null)
                    {
                        selectors.Add((selector, new List<string>(tagSelectorRule.Tags)));
                    }
                }
                else if (rules[data.ruleNumber] is TagConditionRule tagRule)
                {
                    if (!m_tags.TryGetValue((typeIndex, fieldNumber), out (List<string> zeroTags, List<string> nonZeroTags) tags))
                    {
                        tags = (new(), new());
                    }

                    if (tagRule.TagIfNonZero)
                    {
                        foreach (string tag in tagRule.Tags)
                        {
                            tags.nonZeroTags.Add(tag);
                        }
                    }
                    else
                    {
                        foreach (string tag in tagRule.Tags)
                        {
                            tags.zeroTags.Add(tag);
                        }
                    }

                    m_tags[(typeIndex, fieldNumber)] = tags;
                }
            };

            for (int typeIndex = 0; typeIndex < typeSystem.NumberOfTypeIndices; typeIndex++)
            {
                matcher.ForAllMatchingFields(typeIndex, processField);
            }
        }

        static readonly int s_fieldIsArraySentinel = Int32.MaxValue;

        Selector BindSelector(Rule rule, int typeIndex, string[] fieldNames, bool isDynamic)
        {
            List<(int typeIndex, int fieldNumber)> fieldPath = new(fieldNames.Length);
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
                        m_logger.Log(LOG_SOURCE, rule.Location,
                            $"field was expected to be an array type; found {m_typeSystem.QualifiedName(currentTypeIndex)}");
                        return default;
                    }

                    fieldTypeIndex = m_typeSystem.BaseOrElementTypeIndex(currentTypeIndex);
                }
                else
                {
                    (int baseTypeIndex, fieldNumber) = m_typeSystem.GetFieldNumber(currentTypeIndex, fieldNames[i]);
                    if (fieldNumber == -1)
                    {
                        if (!isDynamic)
                        {
                            m_logger.Log(LOG_SOURCE, rule.Location,
                                $"field {fieldNames[i]} not found in type {m_typeSystem.QualifiedName(currentTypeIndex)}; switching to dynamic lookup");
                        }

                        var dynamicFieldNames = new string[fieldNames.Length - i];
                        for (int j = i; j < fieldNames.Length; j++)
                        {
                            dynamicFieldNames[j - i] = fieldNames[j];
                        }
                        return new Selector { StaticPrefix = fieldPath, DynamicTail = dynamicFieldNames };
                    }

                    currentTypeIndex = baseTypeIndex;
                    fieldTypeIndex = m_typeSystem.FieldType(currentTypeIndex, fieldNumber);
                }

                fieldPath.Add((currentTypeIndex, fieldNumber));
                currentTypeIndex = fieldTypeIndex;
            }

            if (m_typeSystem.IsValueType(currentTypeIndex))
            {
                m_logger.Log(LOG_SOURCE, rule.Location,
                    string.Format("field path for {0}.{1} ending in non-reference type field {2} of type {3}",
                        m_typeSystem.QualifiedName(typeIndex),
                        fieldNames[0],
                        fieldNames[^1],
                        m_typeSystem.QualifiedName(currentTypeIndex)));
                return default;
            }

            if (isDynamic)
            {
                m_logger.Log(LOG_SOURCE, rule.Location,
                    string.Format("field path for {0}.{1} uses *_DYNAMIC rule, but is statically resolved",
                        m_typeSystem.QualifiedName(typeIndex),
                        fieldNames[0]));
            }

            return new Selector { StaticPrefix = fieldPath };
        }

        internal PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
        {
            return m_specialReferences.GetValueOrDefault((typeIndex, fieldNumber), PointerFlags.None);
        }

        internal List<Selector> GetConditionAnchorSelectors(int typeIndex, int fieldNumber)
        {
            return m_conditionAnchors[(typeIndex, fieldNumber)];
        }

        internal List<(Selector selector, List<string> tag)> GetTagAnchorSelectors(int typeIndex, int fieldNumber)
        {
            return m_tagAnchors[(typeIndex, fieldNumber)];
        }

        internal (List<string> zeroTags, List<string> nonZeroTags) GetTags(int typeIndex, int fieldNumber)
        {
            return m_tags[(typeIndex, fieldNumber)];
        }
    }
}
