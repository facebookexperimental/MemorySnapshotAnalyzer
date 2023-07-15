// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    sealed class RuleBasedReferenceClassifier : ReferenceClassifier
    {
        readonly BoundRuleset m_boundRuleset;

        internal RuleBasedReferenceClassifier(BoundRuleset boundRuleset)
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
        readonly ReferenceClassifierStore m_store;
        readonly SortedSet<string> m_enabledGroups;

        public RuleBasedReferenceClassifierFactory(ReferenceClassifierStore store, SortedSet<string> enabledGroups)
        {
            m_store = store;
            m_enabledGroups = enabledGroups;
        }

        public override ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new RuleBasedReferenceClassifier(m_store.Bind(typeSystem, m_enabledGroups));
        }
    }
}
