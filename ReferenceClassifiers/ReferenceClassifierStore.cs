/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System.IO;

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

        public HashSet<string> LoadFromFile(string filename, string? groupNamePrefix)
        {
            Dictionary<string, List<Rule>> groupedRules = ReferenceClassifierParser.Load(filename, groupNamePrefix);
            return AddGroupedRules(groupedRules);
        }

        public HashSet<string> LoadFromDllDirectory(string dllDirectory, ILogger logger, string? groupNamePrefix)
        {
            Dictionary<string, List<Rule>> groupedRules = new();
            foreach (string dllFilename in Directory.GetFiles(dllDirectory, "*.dll"))
            {
                ReferenceClassifierMetadataReader.LoadFromDllFilename(dllFilename, groupNamePrefix, logger, groupedRules);
            }
            return AddGroupedRules(groupedRules);
        }

        HashSet<string> AddGroupedRules(Dictionary<string, List<Rule>> groupedRules)
        {
            HashSet<string> loadedGroups = new();
            foreach ((string groupName, List<Rule> rules) in groupedRules)
            {
                GetOrCreateGroup(groupName).Add(rules);
                loadedGroups.Add(groupName);
            }
            return loadedGroups;
        }

        internal BoundRuleset Bind(TypeSystem typeSystem, SortedSet<string> activeGroups, ILogger logger)
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
            return new BoundRuleset(typeSystem, rules, logger);
        }
    }
}
