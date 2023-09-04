/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using NUnit.Framework;

namespace MemorySnapshotAnalyzer.ReferenceClassifiersTests
{
    [TestFixture]
    public sealed class RulesTest
    {
        [Test]
        public void TestTypeSpec()
        {
            TypeSpec typeSpec = new("foo.dll", "bar.type");
            Assert.That(typeSpec.Assembly, Is.EqualTo("foo"));
            Assert.That(typeSpec.TypeName, Is.EqualTo("bar.type"));

            typeSpec = new("Foo.Bar.DLL", "bar.type");
            Assert.That(typeSpec.Assembly, Is.EqualTo("Foo.Bar"));
            Assert.That(typeSpec.TypeName, Is.EqualTo("bar.type"));

            typeSpec = new("foo", "bar.type");
            Assert.That(typeSpec.Assembly, Is.EqualTo("foo"));
            Assert.That(typeSpec.TypeName, Is.EqualTo("bar.type"));

            typeSpec = TypeSpec.Parse("foo.dll:bar.type");
            Assert.That(typeSpec.Assembly, Is.EqualTo("foo"));
            Assert.That(typeSpec.TypeName, Is.EqualTo("bar.type"));

            Assert.Throws<ArgumentException>(() => TypeSpec.Parse("foo"));

            Assert.That(typeSpec.AssemblyMatches("foo"), Is.True);
            Assert.That(typeSpec.AssemblyMatches("FOO"), Is.True);

            Assert.That(typeSpec.ToString(), Is.EqualTo("\"foo:bar.type\""));
        }

        [Test]
        public void TestOwnsRule()
        {
            TypeSpec typeSpec = new("mydll", "mytype");
            OwnsRule rule = new("mylocation", typeSpec, selector: "foo", weight: 0, isDynamic: false);
            Assert.That(rule.Location, Is.EqualTo("mylocation"));
            Assert.That(rule.TypeSpec, Is.EqualTo(typeSpec));

            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo" }));
            Assert.That(rule.Weight, Is.EqualTo(0));
            Assert.That(rule.IsDynamic, Is.False);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" OWNS \"foo\";"));

            rule = new("mylocation", typeSpec, selector: "foo[]", weight: 1, isDynamic: false);
            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo", "[]" }));
            Assert.That(rule.Weight, Is.EqualTo(1));
            Assert.That(rule.IsDynamic, Is.False);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" OWNS(1) \"foo[]\";"));

            rule = new("mylocation", typeSpec, selector: "foo_.bar", weight: -1, isDynamic: false);
            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo_", "bar" }));
            Assert.That(rule.Weight, Is.EqualTo(-1));
            Assert.That(rule.IsDynamic, Is.False);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" OWNS(-1) \"foo_.bar\";"));

            rule = new("mylocation", typeSpec, selector: "foo_._bar.zot", weight: 0, isDynamic: true);
            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo_", "_bar", "zot" }));
            Assert.That(rule.Weight, Is.EqualTo(0));
            Assert.That(rule.IsDynamic, Is.True);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" OWNS_DYNAMIC \"foo_._bar.zot\";"));

            rule = new("mylocation", typeSpec, selector: "foo[]._bar", weight: 1, isDynamic: true);
            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo", "[]", "_bar" }));
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" OWNS_DYNAMIC(1) \"foo[]._bar\";"));

            rule = new("mylocation", typeSpec, selector: "foo[].bar_[]", weight: -1, isDynamic: true);
            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo", "[]", "bar_", "[]" }));
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" OWNS_DYNAMIC(-1) \"foo[].bar_[]\";"));
        }

        [Test]
        public void TestExternalRule()
        {
            TypeSpec typeSpec = new("mydll", "mytype");
            ExternalRule rule = new("mylocation", typeSpec, fieldPattern: "foo*");
            Assert.That(rule.Location, Is.EqualTo("mylocation"));
            Assert.That(rule.TypeSpec, Is.EqualTo(typeSpec));

            Assert.That(rule.FieldPattern, Is.EqualTo("foo*"));
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" EXTERNAL \"foo*\";"));
        }

        [Test]
        public void TestTagSelectorRule()
        {
            TypeSpec typeSpec = new("mydll", "mytype");
            TagSelectorRule rule = new("mylocation", typeSpec, selector: "foo", tags: "tag", isDynamic: false);
            Assert.That(rule.Location, Is.EqualTo("mylocation"));
            Assert.That(rule.TypeSpec, Is.EqualTo(typeSpec));

            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo" }));
            Assert.That(rule.Tags, Is.EquivalentTo(new string[] { "tag" }));
            Assert.That(rule.IsDynamic, Is.False);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" TAG(tag) \"foo\";"));

            rule = new("mylocation", typeSpec, selector: "foo[].bar_[]", tags: "tag1, tag2", isDynamic: true);
            Assert.That(rule.Selector, Is.EquivalentTo(new string[] { "foo", "[]", "bar_", "[]" }));
            Assert.That(rule.Tags, Is.EquivalentTo(new string[] { "tag1", "tag2" }));
            Assert.That(rule.IsDynamic, Is.True);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" TAG_DYNAMIC(tag1,tag2) \"foo[].bar_[]\";"));
        }

        [Test]
        public void TestTagConditionRule()
        {
            TypeSpec typeSpec = new("mydll", "mytype");
            TagConditionRule rule = new("mylocation", typeSpec, fieldPattern: "foo", tags: "tag", tagIfNonZero: false);
            Assert.That(rule.Location, Is.EqualTo("mylocation"));
            Assert.That(rule.TypeSpec, Is.EqualTo(typeSpec));

            Assert.That(rule.FieldPattern, Is.EqualTo("foo"));
            Assert.That(rule.Tags, Is.EquivalentTo(new string[] { "tag" }));
            Assert.That(rule.TagIfNonZero, Is.False);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" TAG_IF_ZERO(tag) \"foo\";"));

            rule = new("mylocation", typeSpec, fieldPattern: "foo*", tags: "tag1,tag2", tagIfNonZero: true);
            Assert.That(rule.FieldPattern, Is.EqualTo("foo*"));
            Assert.That(rule.Tags, Is.EquivalentTo(new string[] { "tag1", "tag2" }));
            Assert.That(rule.TagIfNonZero, Is.True);
            Assert.That(rule.ToString(), Is.EqualTo("\"mydll:mytype\" TAG_IF_NONZERO(tag1,tag2) \"foo*\";"));
        }
    }
}
