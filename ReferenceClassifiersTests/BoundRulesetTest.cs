/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using MemorySnapshotAnalyzer.AbstractMemorySnapshotTests;
using MemorySnapshotAnalyzer.ReferenceClassifiers;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.ReferenceClassifiersTests
{
    [TestFixture]
    public sealed class BoundRulesetTest
    {
        [Test]
        public void TestPointerFlags()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PointerFlags.Weighted.WithoutWeight(), Is.EqualTo(default(PointerFlags)));
                Assert.That(PointerFlags.Weighted.WithWeight(5).Weight(), Is.EqualTo(5));
                Assert.That(PointerFlags.IsExternalReference.WithWeight(5).WithoutWeight(), Is.EqualTo(PointerFlags.IsExternalReference));
                Assert.That(PointerFlags.Weighted.WithWeight(2).CombineWith(PointerFlags.IsExternalReference.WithWeight(1)),
                    Is.EqualTo(PointerFlags.IsExternalReference.WithWeight(2)));
            });
        }

        static List<string> GetLog(MemoryLogger memoryLogger)
        {
            MockStructuredOutput output = new();
            memoryLogger!.Flush(output);
            return output.ExtractDisplayStringLines();
        }

        [Test]
        public void TestBasic()
        {
            TestTypeSystem typeSystem = new();
            MemoryLogger logger = new();
            List<Rule> rules = new()
            {
                new OwnsRule("regularOwnsRule", new TypeSpec("Test.Assembly", "ObjectTwoPointers"), selector: "object2", weight: 2, isDynamic: false),

                new OwnsRule("weakOwnsRule", new TypeSpec("test2.assembly2", "DerivedFromReferenceClassifier"), selector: "derivedField", weight: -1, isDynamic: false),

                new TagConditionRule("taggedUntraced", new TypeSpec("Test.Assembly.dll", "ObjectNoPointers"), fieldPattern: "primitive", tags: "fooTag", tagIfNonZero: true),

                new TagConditionRule("external", new TypeSpec("Test.Assembly", "FieldWithPointerFlagsExternal"), fieldPattern: "object1", tags: "barTag,zotTag", tagIfNonZero: false),
                new TagConditionRule("external", new TypeSpec("Test.Assembly", "FieldWithPointerFlagsExternal"), fieldPattern: "object1", tags: "foo2Tag", tagIfNonZero: true),
                new ExternalRule("externalResetsUntraced", new TypeSpec("Test.Assembly", "FieldWithPointerFlagsExternal"), fieldPattern: "object1"),

                new OwnsRule("combiningWeights", new TypeSpec("assembly3", "WeightedReferences"), selector: "regular", weight: 4, isDynamic: false),
                new OwnsRule("combiningWeights", new TypeSpec("Assembly3.DLL", "WeightedReferences"), selector: "regular", weight: 8, isDynamic: false),

                new OwnsRule("selectorOwnsRule1", new TypeSpec("Test2.Assembly2.DLL", "ReferenceClassifiers"), selector: "weightAnchorStatic.object2", weight: 1, isDynamic: false),
                new OwnsRule("selectorOwnsRule2", new TypeSpec("Test2.Assembly2.DLL", "ReferenceClassifiers"), selector: "weightAnchorStatic.object2.notFound", weight: 1, isDynamic: true),

                new TagSelectorRule("regexTagSelector", TypeSpec.FromRegex("ei..ted"), selector: "strong.notFound", tags: "a,b", isDynamic: true),
            };
            BoundRuleset boundRuleset = new(typeSystem, rules, logger);

            Assert.Multiple(() =>
            {
                Assert.That(GetLog(logger), Has.Count.EqualTo(0));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.ObjectTwoPointers, 1), Is.EqualTo(PointerFlags.Weighted.WithWeight(2)));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.DerivedFromReferenceClassifier, 0), Is.EqualTo(PointerFlags.Weighted.WithWeight(-1)));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.ObjectNoPointers, 0), Is.EqualTo(PointerFlags.TagIfNonZero | PointerFlags.Untraced));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.FieldWithPointerFlagsExternal, 0), Is.EqualTo(PointerFlags.IsExternalReference | PointerFlags.TagIfZero | PointerFlags.TagIfNonZero));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.WeightedReferences, 0), Is.EqualTo(PointerFlags.Weighted.WithWeight(8)));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.ReferenceClassifiers, 0), Is.EqualTo(PointerFlags.IsWeightAnchor));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.WeightedReferences, 1), Is.EqualTo(PointerFlags.IsTagAnchor));
            });

            List<(Selector selector, int weight, string location)> weightAnchorSelectors = boundRuleset.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 0);
            Assert.Multiple(() =>
            {
                Assert.That(weightAnchorSelectors, Has.Count.EqualTo(2));

                Assert.That(weightAnchorSelectors[0].selector.StaticPrefix, Has.Count.EqualTo(2));
                Assert.That(weightAnchorSelectors[0].selector.DynamicTail, Is.Null);
                Assert.That(weightAnchorSelectors[0].location, Is.EqualTo("selectorOwnsRule1"));

                Assert.That(weightAnchorSelectors[1].selector.StaticPrefix, Has.Count.EqualTo(2));
                Assert.That(weightAnchorSelectors[1].selector.DynamicTail, Is.Not.Null);
                Assert.That(weightAnchorSelectors[1].selector.DynamicTail!, Has.Length.EqualTo(1));
                Assert.That(weightAnchorSelectors[1].location, Is.EqualTo("selectorOwnsRule2"));
            });

            List<(Selector selector, List<string> tags, string location)> tagAnchorSelectors = boundRuleset.GetTagAnchorSelectors((int)TestTypeIndex.WeightedReferences, 1);
            Assert.Multiple(() =>
            {
                Assert.That(tagAnchorSelectors, Has.Count.EqualTo(1));
                Assert.That(tagAnchorSelectors[0].location, Is.EqualTo("regexTagSelector"));
            });

            (List<string> zeroTags, List<string> nonZeroTags) = boundRuleset.GetTags((int)TestTypeIndex.ObjectNoPointers, 0);
            Assert.Multiple(() =>
            {
                Assert.That(zeroTags, Is.EquivalentTo(Array.Empty<string>()));
                Assert.That(nonZeroTags, Is.EquivalentTo(new string[] { "fooTag" }));
            });

            (zeroTags, nonZeroTags) = boundRuleset.GetTags((int)TestTypeIndex.FieldWithPointerFlagsExternal, 0);
            Assert.Multiple(() =>
            {
                Assert.That(zeroTags, Is.EquivalentTo(new string[] { "barTag", "zotTag" }));
                Assert.That(nonZeroTags, Is.EquivalentTo(new string[] { "foo2Tag" }));
            });
        }

        [Test]
        public void TestParentFieldNotFound()
        {
            TestTypeSystem typeSystem = new();
            MemoryLogger logger = new();
            List<Rule> rules = new()
            {
                new OwnsRule("parentFieldWillNotBeFound", new TypeSpec("Test.Assembly", "DerivedFromReferenceClassifier"), selector: "array", weight: 5, isDynamic: false),
            };
            BoundRuleset boundRuleset = new(typeSystem, rules, logger);

            Assert.Multiple(() =>
            {
                // TODO: silently ignoring this rule is not ideal; there are two potentially better choices:
                // (1) get a warning that the rule matched a type but never matched a field
                // (2) find the field in the parent type and set the weight for (TestTypeIndex.ReferenceClassifiers, 5)
                Assert.That(GetLog(logger), Has.Count.EqualTo(0));
                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.ReferenceClassifiers, 5), Is.EqualTo(default(PointerFlags)));
            });
        }

        [Test]
        public void TestSelectorBindingIssues()
        {
            TestTypeSystem typeSystem = new();
            MemoryLogger logger = new();
            List<Rule> rules = new()
            {
                new OwnsRule("selectorOwnsRule2", new TypeSpec("Test2.Assembly2.DLL", "ReferenceClassifiers"), selector: "weightAnchorStatic.object2[]", weight: 1, isDynamic: false),

                new TagSelectorRule("regexTagSelector", TypeSpec.FromRegex("ei..ted"), selector: "strong[]", tags: "a,b", isDynamic: false),
            };
            BoundRuleset boundRuleset = new(typeSystem, rules, logger);

            List<string> messages = GetLog(logger);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(2));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.ReferenceClassifiers, 0), Is.EqualTo(PointerFlags.IsWeightAnchor));
                List<(Selector selector, int weight, string location)> weightAnchorSelectors = boundRuleset.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 0);
                Assert.That(weightAnchorSelectors, Has.Count.EqualTo(0));

                Assert.That(boundRuleset.GetPointerFlags((int)TestTypeIndex.WeightedReferences, 1), Is.EqualTo(PointerFlags.IsTagAnchor));
                List<(Selector selector, List<string> tags, string location)> tagAnchorSelectors = boundRuleset.GetTagAnchorSelectors((int)TestTypeIndex.WeightedReferences, 1);
                Assert.That(tagAnchorSelectors, Has.Count.EqualTo(0));
            });
        }
    }
}
