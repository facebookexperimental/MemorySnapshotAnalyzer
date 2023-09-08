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
        readonly Dictionary<(int typeIndex, int fieldNumber), List<(Selector selector, int weight, string location)>> m_weightAnchors;
        readonly Dictionary<(int typeIndex, int fieldNumber), List<(Selector selector, List<string> tag, string location)>> m_tagAnchors;
        readonly Dictionary<(int typeIndex, int fieldNumber), (List<string> zeroTags, List<string> nonZeroTags)> m_tags;

        internal BoundRuleset(TypeSystem typeSystem, List<Rule> rules, ILogger logger)
        {
            m_typeSystem = typeSystem;
            m_logger = logger;
            m_specialReferences = new();
            m_weightAnchors = new();
            m_tagAnchors = new();
            m_tags = new();

            m_logger.Clear(LOG_SOURCE);

            List<(TypeSpec spec, string fieldPattern, (int ruleNumber, PointerFlags pointerFlags))> specs = new();
            for (int ruleNumber = 0; ruleNumber < rules.Count; ruleNumber++)
            {
                if (rules[ruleNumber] is OwnsRule ownsRule)
                {
                    PointerFlags pointerFlags =
                        ownsRule.Selector.Length > 1 ? PointerFlags.IsWeightAnchor : PointerFlags.Weighted.WithWeight(ownsRule.Weight);
                    specs.Add((ownsRule.TypeSpec, ownsRule.Selector[0], (ruleNumber, pointerFlags)));
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
                PointerFlags newPointerFlags = existingPointerFlags.CombineWith(data.pointerFlags);
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
                    if (!m_weightAnchors.TryGetValue((typeIndex, fieldNumber), out List<(Selector selector, int weight, string location)>? selectors))
                    {
                        selectors = new();
                        m_weightAnchors.Add((typeIndex, fieldNumber), selectors);
                    }

                    var selector = m_typeSystem.BindSelector(s => m_logger.Log(LOG_SOURCE, ownsRule.Location, s),
                        typeIndex, ownsRule.Selector, expectDynamic: ownsRule.IsDynamic, expectReferenceType: true);
                    if (selector.StaticPrefix != null)
                    {
                        selectors.Add((selector, weight: ownsRule.Weight, location: ownsRule.Location));
                    }
                }
                else if (rules[data.ruleNumber] is TagSelectorRule tagSelectorRule)
                {
                    if (!m_tagAnchors.TryGetValue((typeIndex, fieldNumber), out List<(Selector selector, List<string> tags, string location)>? selectors))
                    {
                        selectors = new();
                        m_tagAnchors.Add((typeIndex, fieldNumber), selectors);
                    }

                    var selector = m_typeSystem.BindSelector(s => m_logger.Log(LOG_SOURCE, tagSelectorRule.Location, s),
                        typeIndex, tagSelectorRule.Selector, tagSelectorRule.IsDynamic, expectReferenceType: true);
                    if (selector.StaticPrefix != null)
                    {
                        selectors.Add((selector, tags: new List<string>(tagSelectorRule.Tags), location: tagSelectorRule.Location));
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

        internal PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
        {
            return m_specialReferences.GetValueOrDefault((typeIndex, fieldNumber), default(PointerFlags));
        }

        internal List<(Selector selector, int weight, string location)> GetWeightAnchorSelectors(int typeIndex, int fieldNumber)
        {
            return m_weightAnchors[(typeIndex, fieldNumber)];
        }

        internal List<(Selector selector, List<string> tag, string location)> GetTagAnchorSelectors(int typeIndex, int fieldNumber)
        {
            return m_tagAnchors[(typeIndex, fieldNumber)];
        }

        internal (List<string> zeroTags, List<string> nonZeroTags) GetTags(int typeIndex, int fieldNumber)
        {
            return m_tags[(typeIndex, fieldNumber)];
        }
    }
}
