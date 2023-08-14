/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiers
{
    public sealed class ReferenceClassifierGroup
    {
        readonly string m_name;
        readonly List<Rule> m_rules;

        public ReferenceClassifierGroup(string name)
        {
            m_name = name;
            m_rules = new();
        }

        public string Name => m_name;

        public int NumberOfRules => m_rules.Count;

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
