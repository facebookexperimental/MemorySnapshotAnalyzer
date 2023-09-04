/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using NUnit.Framework;

namespace MemorySnapshotAnalyzer.ReferenceClassifiersTests
{
    [TestFixture]
    public sealed class ReferenceClassifierParserTest
    {
        [Test]
        public void TestParser()
        {
            Dictionary<string, List<Rule>> groupedRules = new();
            ReferenceClassifierParser.Load("test_parser.rcl", groupNamePrefix: null, groupedRules);
            Assert.That(groupedRules.Keys, Is.EquivalentTo(new HashSet<string>()
            {
                "anonymous", "subgroup1", "subgroup2", "subgroup1.imported1", "imported2"
            }));

            CheckGroup(groupedRules["anonymous"], new List<(string location, string rulesText)>()
            {
                (location: "test_parser.rcl:1", rulesText: "\"mydll:mytype\" OWNS \"foo\";"),
                (location: "test_parser.rcl:2", rulesText: "\"mydll:mytype\" OWNS(1) \"foo[]\";"),
                (location: "test_parser.rcl:12", rulesText: "\"MyNamespace.MyDll:mytype\" OWNS_DYNAMIC \"foo_._bar.zot\";"),
                (location: "test_parser_import2.rcl:1", rulesText: "\"imported2:imported2\" OWNS \"imported2_1\";"),
                (location: "test_parser_import2.rcl:9", rulesText: "\"imported2:imported2\" OWNS \"imported2_3\";"),
                (location: "test_parser.rcl:17", rulesText: "\"mydll:mytype\" OWNS_DYNAMIC(-1) \"foo[].bar_[]\";"),
                (location: "test_parser.rcl:18", rulesText: "\"mydll:mytype\" EXTERNAL \"foo*\";"),
                (location: "test_parser.rcl:18", rulesText: "\"mydll:mytype\" TAG_IF_NONZERO(tag1,tag2) \"foo*\";"),
                (location: "test_parser.rcl:19", rulesText: "\"mydll:mytype\" TAG(tag) \"foo\";"),
                (location: "test_parser.rcl:19", rulesText: "\"mydll:mytype\" TAG_IF_ZERO(tag) \"foo\";"),
            });

            CheckGroup(groupedRules["subgroup1"], new List<(string location, string rulesText)>()
            {
                (location: "test_parser.rcl:6", rulesText: "\"MyNamespace.MyDll:mytype\" OWNS(-1) \"foo_.bar\";"),
            });

            CheckGroup(groupedRules["subgroup1.imported1"], new List<(string location, string rulesText)>()
            {
                (location: "test_parser_import1.rcl:3", rulesText: "\"imported1:imported1\" OWNS \"imported1\";"),
            });

            CheckGroup(groupedRules["imported2"], new List<(string location, string rulesText)>()
            {
                (location: "test_parser_import2.rcl:5", rulesText: "\"imported2:imported2\" OWNS \"imported2_2\";"),
            });

            CheckGroup(groupedRules["subgroup2"], new List<(string location, string rulesText)>()
            {
                (location: "test_parser.rcl:23", rulesText: "\"mydll:mytype\" TAG_DYNAMIC(tag1,tag2) \"foo[].bar_[]\";"),
            });
        }

        [Test]
        public void TestGroupPrefix()
        {
            Dictionary<string, List<Rule>> groupedRules = new();
            ReferenceClassifierParser.Load("test_parser.rcl", groupNamePrefix: "prefix", groupedRules);
            Assert.That(groupedRules.Keys, Is.EquivalentTo(new HashSet<string>()
            {
                "prefix", "prefix.subgroup1", "prefix.subgroup2", "prefix.subgroup1.imported1", "prefix.imported2"
            }));
        }

        void CheckGroup(List<Rule> rules, List<(string location, string rulesText)> expected)
        {
            Assert.That(rules.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < rules.Count; i++)
            {
                Assert.That(rules[i].Location, Is.EqualTo(expected[i].location));
                Assert.That(rules[i].ToString(), Is.EqualTo(expected[i].rulesText));
            }
        }
    }
}
