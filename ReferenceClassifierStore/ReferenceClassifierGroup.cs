// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class ReferenceClassifierGroup
    {
        readonly string m_name;
        readonly List<Rule> m_rules;
        bool m_enabled;

        public ReferenceClassifierGroup(string name)
        {
            m_name = name;
            m_enabled = true;
            m_rules = new();
        }

        public string Name => m_name;

        public bool SetIsEnabled(bool newEnabled)
        {
            if (m_enabled == newEnabled)
            {
                return false;
            }
            else
            {
                m_enabled = newEnabled;
                return true;
            }
        }

        public void Add(IEnumerable<Rule> rules)
        {
            foreach (Rule rule in rules)
            {
                m_rules.Add(rule);
            }
        }

        public IEnumerable<Rule> GetRules()
        {
            return m_rules;
        }
    }
}
