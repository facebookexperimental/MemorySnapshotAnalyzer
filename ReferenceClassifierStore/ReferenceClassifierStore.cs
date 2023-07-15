﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class ReferenceClassifierStore
    {
        readonly SortedDictionary<string, ReferenceClassifierGroup> m_referenceClassifierGroups;

        public ReferenceClassifierStore()
        {
            m_referenceClassifierGroups = new();
        }

        public IEnumerable<ReferenceClassifierGroup> AllGroups()
        {
            foreach ((string groupName, ReferenceClassifierGroup group) in m_referenceClassifierGroups)
            {
                yield return group;
            }
        }

        public void Clear()
        {
            m_referenceClassifierGroups.Clear();
        }

        public void ClearGroup(string groupName)
        {
            m_referenceClassifierGroups.Remove(groupName);
        }

        public bool TryGetGroup(string groupName, out ReferenceClassifierGroup? group)
        {
            return m_referenceClassifierGroups.TryGetValue(groupName, out group);
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

        public HashSet<string> Load(string filename, string? overrideGroupName)
        {
            Dictionary<string, List<Rule>> groupedRules = ReferenceClassifierLoader.Load(filename);
            HashSet<string> loadedGroups = new();
            foreach ((string groupName, List<Rule> rules) in groupedRules)
            {
                string effectiveGroupName = overrideGroupName ?? groupName;
                GetOrCreateGroup(effectiveGroupName).Add(rules);
                loadedGroups.Add(effectiveGroupName);
            }
            return loadedGroups;
        }

        internal BoundRuleset Bind(TypeSystem typeSystem, SortedSet<string> activeGroups)
        {
            List<Rule> rules = new();
            foreach ((string groupName, ReferenceClassifierGroup group) in m_referenceClassifierGroups)
            {
                if (activeGroups.Contains(groupName))
                {
                    foreach (Rule rule in group.GetRules())
                    {
                        rules.Add(rule);
                    }
                }
            }
            return new BoundRuleset(typeSystem, rules);
        }
    }
}