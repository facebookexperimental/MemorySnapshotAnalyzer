/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshotTests
{
    [TestFixture]
    public sealed class TypeSystemTests
    {
        TestTypeSystem? m_typeSystem;

        [SetUp]
        public void SetUp()
        {
            m_typeSystem = new TestTypeSystem();
        }

        [Test]
        public void TestQualifiedGenericNameWithArity()
        {
            Assert.That(m_typeSystem!.QualifiedGenericNameWithArity((int)TestTypeIndex.ObjectArray),
                Is.EqualTo("ObjectNoPointers[]"));
            Assert.That(m_typeSystem!.QualifiedGenericNameWithArity((int)TestTypeIndex.GenericTypeWithNesting),
                Is.EqualTo("GenericTypeWithNesting`2"));
            Assert.That(m_typeSystem!.QualifiedGenericNameWithArity((int)TestTypeIndex.GenericTypeArray),
                Is.EqualTo("GenericTypeArray<int>[]"));

            // Redirect to another type, to confirm that a repeated call returns a cached computed value.
            m_typeSystem.SetTargetForConfigurableTypeIndex(TestTypeIndex.GenericTypeWithNesting);
            Assert.That(m_typeSystem!.QualifiedGenericNameWithArity((int)TestTypeIndex.Configurable),
                Is.EqualTo("GenericTypeWithNesting`2"));
            m_typeSystem.SetTargetForConfigurableTypeIndex(TestTypeIndex.ObjectArray);
            Assert.That(m_typeSystem!.QualifiedGenericNameWithArity((int)TestTypeIndex.Configurable),
                Is.EqualTo("GenericTypeWithNesting`2"));

            Assert.That(m_typeSystem!.QualifiedGenericNameWithArity((int)TestTypeIndex.EmptyTypeNameCornerCase),
                Is.EqualTo(""));
        }

        [Test]
        public void TestGetPointerOffsets()
        {
            int baseOffset = 16;

            List<PointerInfo<int>> pointerInfos;

            pointerInfos = m_typeSystem!.GetPointerOffsets((int)TestTypeIndex.Primitive, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(0));

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ObjectNoPointers, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(0));

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ValueTypeTwoPointers, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(2));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            }

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ObjectTwoPointers, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(2));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            }

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ObjectTwoPointersInValueType, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(3));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));

                Assert.That(pointerInfos[2].Value, Is.EqualTo(baseOffset + 16));
                Assert.That(pointerInfos[2].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[2].TypeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointersInValueType));
                Assert.That(pointerInfos[2].FieldNumber, Is.EqualTo(1));
            }

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.DerivedTypeThreePointers, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(3));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));

                Assert.That(pointerInfos[2].Value, Is.EqualTo(baseOffset + 16));
                Assert.That(pointerInfos[2].PointerFlags, Is.EqualTo(PointerFlags.Weighted.WithWeight(1)));
                Assert.That(pointerInfos[2].TypeIndex, Is.EqualTo((int)TestTypeIndex.DerivedTypeThreePointers));
                Assert.That(pointerInfos[2].FieldNumber, Is.EqualTo(0));
            }

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ClassWithStaticFields, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(3));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));

                Assert.That(pointerInfos[2].Value, Is.EqualTo(baseOffset + 16));
                Assert.That(pointerInfos[2].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[2].TypeIndex, Is.EqualTo((int)TestTypeIndex.ClassWithStaticFields));
                Assert.That(pointerInfos[2].FieldNumber, Is.EqualTo(2));
            }

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.FieldWithPointerFlagsExternal, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(2));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(PointerFlags.IsExternalReference));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.FieldWithPointerFlagsExternal));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.FieldWithPointerFlagsExternal));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            }
        }

        [Test]
        public void TestGetPointerOffsetsDoesCaching()
        {
            int baseOffset = 16;

            List<PointerInfo<int>> pointerInfos;

            m_typeSystem!.SetTargetForConfigurableTypeIndex(TestTypeIndex.ObjectTwoPointers);

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.Configurable, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(2));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            }

            // Redirect to a type without pointers, to confirm that a repeated call to GetPointerOffsets returns cached offsets.
            m_typeSystem.SetTargetForConfigurableTypeIndex(TestTypeIndex.Primitive);

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.Configurable, baseOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(2));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            }
        }

        [Test]
        public void TestGetStaticFieldPointerOffsets()
        {
            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetStaticFieldPointerOffsets((int)TestTypeIndex.ClassWithStaticFields, fieldNumber: 1).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(1));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ClassWithStaticFields));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(1));
            }

            pointerInfos = m_typeSystem.GetStaticFieldPointerOffsets((int)TestTypeIndex.ClassWithStaticFields, fieldNumber: 3).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(2));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            }
        }

        [Test]
        public void TestGetArrayElementPointerOffsets()
        {
            int elementTypeIndex = m_typeSystem!.BaseOrElementTypeIndex((int)TestTypeIndex.ObjectArray);
            int arrayElementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, 1);
            List<PointerInfo<int>> pointerInfos = m_typeSystem.GetArrayElementPointerOffsets(elementTypeIndex, arrayElementOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(1));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(arrayElementOffset + 0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo(elementTypeIndex));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(-1));
            }

            elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex((int)TestTypeIndex.ValueTypeArray);
            arrayElementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, 1);
            pointerInfos = m_typeSystem.GetArrayElementPointerOffsets(elementTypeIndex, arrayElementOffset).ToList();
            Assert.That(pointerInfos.Count, Is.EqualTo(2));
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(arrayElementOffset + 0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo(elementTypeIndex));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(arrayElementOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo(elementTypeIndex));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            }
        }

        [Test]
        public void TestGetFieldNumber()
        {
            int typeIndex, fieldNumber;

            (typeIndex, fieldNumber) = m_typeSystem!.GetFieldNumber((int)TestTypeIndex.ValueTypeTwoPointers, "object1");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo(((int)TestTypeIndex.ValueTypeTwoPointers, 0)));

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.ValueTypeTwoPointers, "nonexistent");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo((-1, -1)));

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.ObjectTwoPointers, "object2");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo(((int)TestTypeIndex.ObjectTwoPointers, 1)));

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.DerivedTypeThreePointers, "object1");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo(((int)TestTypeIndex.ObjectTwoPointers, 0)));

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.ObjectTwoPointers, "Object2");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo((-1, -1)));

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.ObjectArray, "primitive");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo((-1, -1)));
        }

        [Test]
        public void TestGetWeightAnchorSelectors()
        {
            // Before the corresponding PointerFlags have been returned, the ReferenceClassifier instance has not been built from the factory.
            Assert.Throws<NullReferenceException>(() => m_typeSystem!.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 0));

            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetPointerOffsets((int)TestTypeIndex.ReferenceClassifiers, baseOffset: 0).ToList();
            Assert.That(pointerInfos.Count, Is.GreaterThan(0));
            {
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(PointerFlags.IsWeightAnchor));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));
            }

            List<(Selector selector, int weight)> selectors = m_typeSystem.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, fieldNumber: 0).ToList();
            Assert.That(selectors.Count, Is.EqualTo(1));
            {
                Assert.That(selectors[0].selector.StaticPrefix.Count, Is.EqualTo(1));
                Assert.That(selectors[0].selector.DynamicTail, Is.Null);
                Assert.That(selectors[0].weight, Is.EqualTo(0));
            }

            selectors = m_typeSystem.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, fieldNumber: 1).ToList();
            Assert.That(selectors.Count, Is.EqualTo(1));
            {
                Assert.That(selectors[0].selector.StaticPrefix.Count, Is.EqualTo(1));
                Assert.That(selectors[0].selector.DynamicTail, Is.Not.Null);
                Assert.That(selectors[0].selector.DynamicTail!.Count, Is.EqualTo(1));
                Assert.That(selectors[0].weight, Is.EqualTo(3));
            }
        }

        [Test]
        public void TestGetTagAnchorSelectors()
        {
            // Before the corresponding PointerFlags have been returned, the ReferenceClassifier instance has not been built from the factory.
            Assert.Throws<NullReferenceException>(() => m_typeSystem!.GetTagAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 2));

            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetPointerOffsets((int)TestTypeIndex.ReferenceClassifiers, baseOffset: 0).ToList();
            Assert.That(pointerInfos.Count, Is.GreaterThan(2));
            {
                Assert.That(pointerInfos[2].PointerFlags, Is.EqualTo(PointerFlags.IsTagAnchor));
                Assert.That(pointerInfos[2].FieldNumber, Is.EqualTo(2));
            }

            List<(Selector selector, List<string> tags)> result = m_typeSystem.GetTagAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 2).ToList();
            Assert.That(result.Count, Is.EqualTo(1));
            {
                Assert.That(result[0].selector.StaticPrefix.Count, Is.EqualTo(1));
                Assert.That(result[0].tags.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public void TestGetTags()
        {
            // Before the corresponding PointerFlags have been returned, the ReferenceClassifier instance has not been built from the factory.
            Assert.Throws<NullReferenceException>(() => m_typeSystem!.GetTags((int)TestTypeIndex.ReferenceClassifiers, 3));

            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetPointerOffsets((int)TestTypeIndex.ReferenceClassifiers, baseOffset: 0).ToList();
            Assert.That(pointerInfos.Count, Is.GreaterThan(3));
            {
                Assert.That(pointerInfos[3].PointerFlags, Is.EqualTo(PointerFlags.TagIfZero));
                Assert.That(pointerInfos[3].FieldNumber, Is.EqualTo(3));
            }

            (List<string> zeroTags, List<string> nonZeroTags) = m_typeSystem.GetTags((int)TestTypeIndex.ReferenceClassifiers, 3);
            Assert.That(zeroTags.Count, Is.EqualTo(2));
            Assert.That(nonZeroTags.Count, Is.EqualTo(1));
        }
    }
}
