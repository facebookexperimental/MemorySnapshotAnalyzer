// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class RuleBasedReferenceClassifier : ReferenceClassifier
    {
        readonly BoundRuleset m_boundRuleset;

        public RuleBasedReferenceClassifier(BoundRuleset boundRuleset)
        {
            m_boundRuleset = boundRuleset;
        }

        public override bool IsOwningReference(int typeIndex, int fieldNumber)
        {
            return m_boundRuleset.IsOwningReference(typeIndex, fieldNumber);
        }

        public override bool IsConditionAnchor(int typeIndex, int fieldNumber)
        {
            return m_boundRuleset.IsConditionAnchor(typeIndex, fieldNumber);
        }

        public override List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
        {
            return m_boundRuleset.GetConditionalAnchorFieldPaths(typeIndex, fieldNumber);
        }
    }

    public sealed class RuleBasedReferenceClassifierFactory : ReferenceClassifierFactory
    {
        readonly string m_description;
        readonly List<Rule> m_configurationEntries;

        public RuleBasedReferenceClassifierFactory(string description, List<Rule> configurationEntries)
        {
            m_description = description;
            m_configurationEntries = configurationEntries;
        }

        public override ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new RuleBasedReferenceClassifier(new BoundRuleset(typeSystem, m_configurationEntries));
        }

        public override string Description => m_description;
    }
}
