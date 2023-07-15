// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class ReferenceClassifierStore
    {
        private readonly SortedDictionary<string, ReferenceClassifierGroup> m_referenceClassifierGroups;

        public ReferenceClassifierStore()
        {
            m_referenceClassifierGroups = new();
        }

        // TODO: have some descriptive information about groups and rules
        public string Description => "reference classifier store";

        public IEnumerable<ReferenceClassifierGroup> AllGroups()
        {
            foreach ((string groupName, ReferenceClassifierGroup group) in m_referenceClassifierGroups)
            {
                yield return group;
            }
        }

        public ReferenceClassifierGroup GetExistingGroup(string groupName)
        {
            if (!m_referenceClassifierGroups.TryGetValue(groupName, out ReferenceClassifierGroup? group))
            {
                throw new ArgumentException($"unknown reference classifier group \"{groupName}\"");
            }

            return group;
        }

        public ReferenceClassifierGroup GetOrCreateGroup(string groupName)
        {
            if (!m_referenceClassifierGroups.TryGetValue(groupName, out ReferenceClassifierGroup? group))
            {
                group = new ReferenceClassifierGroup(groupName);
                m_referenceClassifierGroups.Add(groupName, group);
            }

            return group;
        }

        public void Load(string filename)
        {
            Dictionary<string, List<Rule>> groupedRules = ReferenceClassifierLoader.Load(filename);
            foreach ((string groupName, List<Rule> rules) in groupedRules)
            {
                GetOrCreateGroup(groupName).Add(rules);
            }
        }

        internal BoundRuleset Bind(TypeSystem typeSystem)
        {
            List<Rule> rules = new();
            foreach ((string groupName, ReferenceClassifierGroup group) in m_referenceClassifierGroups)
            {
                foreach (Rule rule in group.GetRules())
                {
                    rules.Add(rule);
                }
            }
            return new BoundRuleset(typeSystem, rules);
        }
    }
}