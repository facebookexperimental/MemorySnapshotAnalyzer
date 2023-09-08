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
            Assert.That(pointerInfos, Is.Empty);

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ObjectNoPointers, baseOffset).ToList();
            Assert.That(pointerInfos, Is.Empty);

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ValueTypeTwoPointers, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            });

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ObjectTwoPointers, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            });

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ObjectTwoPointersInValueType, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(3).Items);
            Assert.Multiple(() =>
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
            });

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.DerivedTypeFourPointers, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(4).Items);
            Assert.Multiple(() =>
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
                Assert.That(pointerInfos[2].TypeIndex, Is.EqualTo((int)TestTypeIndex.DerivedTypeFourPointers));
                Assert.That(pointerInfos[2].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[3].Value, Is.EqualTo(baseOffset + 24));
                Assert.That(pointerInfos[3].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[3].TypeIndex, Is.EqualTo((int)TestTypeIndex.DerivedTypeFourPointers));
                Assert.That(pointerInfos[3].FieldNumber, Is.EqualTo(1));
            });

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.ClassWithStaticFields, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(3).Items);
            Assert.Multiple(() =>
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
            });

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.FieldWithPointerFlagsExternal, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(PointerFlags.IsExternalReference));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.FieldWithPointerFlagsExternal));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.FieldWithPointerFlagsExternal));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestGetPointerOffsetsDoesCaching()
        {
            int baseOffset = 16;

            List<PointerInfo<int>> pointerInfos;

            m_typeSystem!.SetTargetForConfigurableTypeIndex(TestTypeIndex.ObjectTwoPointers);

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.Configurable, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            });

            // Redirect to a type without pointers, to confirm that a repeated call to GetPointerOffsets returns cached offsets.
            m_typeSystem.SetTargetForConfigurableTypeIndex(TestTypeIndex.Primitive);

            pointerInfos = m_typeSystem.GetPointerOffsets((int)TestTypeIndex.Configurable, baseOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(baseOffset));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(baseOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.Configurable));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestGetStaticFieldPointerOffsets()
        {
            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetStaticFieldPointerOffsets((int)TestTypeIndex.ClassWithStaticFields, fieldNumber: 1).ToList();
            Assert.That(pointerInfos, Has.Exactly(1).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ClassWithStaticFields));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(1));
            });

            pointerInfos = m_typeSystem.GetStaticFieldPointerOffsets((int)TestTypeIndex.ClassWithStaticFields, fieldNumber: 3).ToList();
            Assert.That(pointerInfos, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestGetArrayElementPointerOffsets()
        {
            int elementTypeIndex = m_typeSystem!.BaseOrElementTypeIndex((int)TestTypeIndex.ObjectArray);
            int arrayElementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, 1);
            List<PointerInfo<int>> pointerInfos = m_typeSystem.GetArrayElementPointerOffsets(elementTypeIndex, arrayElementOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(1).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(arrayElementOffset + 0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo(elementTypeIndex));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(-1));
            });

            elementTypeIndex = m_typeSystem.BaseOrElementTypeIndex((int)TestTypeIndex.ValueTypeArray);
            arrayElementOffset = m_typeSystem.GetArrayElementOffset(elementTypeIndex, 1);
            pointerInfos = m_typeSystem.GetArrayElementPointerOffsets(elementTypeIndex, arrayElementOffset).ToList();
            Assert.That(pointerInfos, Has.Exactly(2).Items);
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].Value, Is.EqualTo(arrayElementOffset + 0));
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[0].TypeIndex, Is.EqualTo(elementTypeIndex));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));

                Assert.That(pointerInfos[1].Value, Is.EqualTo(arrayElementOffset + 8));
                Assert.That(pointerInfos[1].PointerFlags, Is.EqualTo(default(PointerFlags)));
                Assert.That(pointerInfos[1].TypeIndex, Is.EqualTo(elementTypeIndex));
                Assert.That(pointerInfos[1].FieldNumber, Is.EqualTo(1));
            });
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

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.DerivedTypeFourPointers, "object1");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo(((int)TestTypeIndex.ObjectTwoPointers, 0)));

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.ObjectTwoPointers, "Object2");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo((-1, -1)));

            (typeIndex, fieldNumber) = m_typeSystem.GetFieldNumber((int)TestTypeIndex.ObjectArray, "primitive");
            Assert.That((typeIndex, fieldNumber), Is.EqualTo((-1, -1)));
        }

        [Test]
        public void TestBindSelector()
        {
            List<string> warnings = new();

            // Base case: single field
            Selector selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ValueTypeTwoPointers,
                new string[] { "object2" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Is.Empty);

                Assert.That(selector.StaticPrefix, Has.Exactly(1).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(1));

                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Two fields looked up statically
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ReferenceClassifiers,
                new string[] { "weightAnchorStatic", "object1" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Is.Empty);

                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ReferenceClassifiers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(0));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(0));

                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Expecting a warning if dynamic lookup expected, but static lookup succeeded
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ReferenceClassifiers,
                new string[] { "weightAnchorStatic", "object1" },
                expectDynamic: true,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(1).Items);
                warnings.Clear();
                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Finding first field in an ancestor type
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.DerivedTypeFourPointers,
                new string[] { "next", "object2" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(0).Items);
                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.DerivedTypeFourPointers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(1));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(1));
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Finding subsequent field in an ancestor type
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.DerivedFromReferenceClassifier,
                new string[] { "object", "object" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(0).Items);
                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ReferenceClassifiers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(4));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ReferenceClassifiers));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(4));
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Finding array type and selecting all elements
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ReferenceClassifiers,
                new string[] { "array", "[]" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(0).Items);
                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ReferenceClassifiers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(5));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ObjectArray));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(Selector.FieldNumberArraySentinel));
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Finding array type of value types, and selecting a field from all elements
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ReferenceClassifiers,
                new string[] { "valueTypeArray", "[]", "object2" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(0).Items);
                Assert.That(selector.StaticPrefix, Has.Exactly(3).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ReferenceClassifiers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(6));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeArray));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(Selector.FieldNumberArraySentinel));
                Assert.That(selector.StaticPrefix[2].typeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(selector.StaticPrefix[2].fieldNumber, Is.EqualTo(1));
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Switching to dynamic lookup on object field not found
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ValueTypeTwoPointers,
                new string[] { "object1", "dynamicField", "[]", "value" },
                expectDynamic: true,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(0).Items);
                Assert.That(selector.StaticPrefix, Has.Exactly(1).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeTwoPointers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(0));
                Assert.That(selector.DynamicTail, Is.EquivalentTo(new string[] { "dynamicField", "[]", "value" }));
            });

            // Same, but expecting static lookup, hence getting a warning
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ValueTypeTwoPointers,
                new string[] { "object1", "dynamicField", "[]", "value" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(1).Items);
                warnings.Clear();
                Assert.That(selector.StaticPrefix, Has.Exactly(1).Items);
                Assert.That(selector.DynamicTail, Is.EquivalentTo(new string[] { "dynamicField", "[]", "value" }));
            });

            // When not finding a field on a value type, not switching to dynamic, but failing with an error
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ObjectTwoPointersInValueType,
                new string[] { "value", "notFound", "value" },
                expectDynamic: true,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(1).Items);
                warnings.Clear();
                Assert.That(selector.StaticPrefix, Is.Null);
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // When [] is applied to a non-array object, fail with an error
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ReferenceClassifiers,
                new string[] { "object", "[]" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(1).Items);
                warnings.Clear();
                Assert.That(selector.StaticPrefix, Is.Null);
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Selector producing a value type, produces a warning if expecting a reference type
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ObjectTwoPointers,
                new string[] { "object1", "primitive" },
                expectDynamic: false,
                expectReferenceType: true);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(1).Items);
                warnings.Clear();
                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ObjectTwoPointers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(0));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ObjectNoPointers));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(0));
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Selector producing a value type, no warning when not expecting a reference type
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ReferenceClassifiers,
                new string[] { "valueTypeArray", "[]" },
                expectDynamic: false,
                expectReferenceType: false);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(0).Items);
                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ReferenceClassifiers));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(6));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ValueTypeArray));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(Selector.FieldNumberArraySentinel));
                Assert.That(selector.DynamicTail, Is.Null);
            });

            // Selector can find static field
            selector = m_typeSystem!.BindSelector(warnings.Add,
                (int)TestTypeIndex.ClassWithStaticFields,
                new string[] { "static1", "primitive" },
                expectDynamic: false,
                expectReferenceType: false);
            Assert.Multiple(() =>
            {
                Assert.That(warnings, Has.Exactly(0).Items);
                Assert.That(selector.StaticPrefix, Has.Exactly(2).Items);
                Assert.That(selector.StaticPrefix[0].typeIndex, Is.EqualTo((int)TestTypeIndex.ClassWithStaticFields));
                Assert.That(selector.StaticPrefix[0].fieldNumber, Is.EqualTo(1));
                Assert.That(selector.StaticPrefix[1].typeIndex, Is.EqualTo((int)TestTypeIndex.ObjectNoPointers));
                Assert.That(selector.StaticPrefix[1].fieldNumber, Is.EqualTo(0));
                Assert.That(selector.DynamicTail, Is.Null);
            });
        }

        [Test]
        public void TestGetWeightAnchorSelectors()
        {
            // Before the corresponding PointerFlags have been returned, the ReferenceClassifier instance has not been built from the factory.
            Assert.Throws<NullReferenceException>(() => m_typeSystem!.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 0));

            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetPointerOffsets((int)TestTypeIndex.ReferenceClassifiers, baseOffset: 0).ToList();
            Assert.That(pointerInfos, Has.Count.GreaterThan(0));
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[0].PointerFlags, Is.EqualTo(PointerFlags.IsWeightAnchor));
                Assert.That(pointerInfos[0].FieldNumber, Is.EqualTo(0));
            });

            List<(Selector selector, int weight, string location)> selectors = m_typeSystem.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, fieldNumber: 0).ToList();
            Assert.That(selectors, Has.Exactly(1).Items);
            Assert.Multiple(() =>
            {
                Assert.That(selectors[0].selector.StaticPrefix, Has.Exactly(1).Items);
                Assert.That(selectors[0].selector.DynamicTail, Is.Null);
                Assert.That(selectors[0].weight, Is.EqualTo(0));
                Assert.That(selectors[0].location, Is.EqualTo($"{(int)TestTypeIndex.ReferenceClassifiers}:0"));
            });

            selectors = m_typeSystem.GetWeightAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, fieldNumber: 1).ToList();
            Assert.That(selectors, Has.Exactly(1).Items);
            Assert.Multiple(() =>
            {
                Assert.That(selectors[0].selector.StaticPrefix, Has.Exactly(1).Items);
                Assert.That(selectors[0].selector.DynamicTail, Is.Not.Null);
                Assert.That(selectors[0].selector.DynamicTail!, Has.Length.EqualTo(1));
                Assert.That(selectors[0].weight, Is.EqualTo(3));
            });
        }

        [Test]
        public void TestGetTagAnchorSelectors()
        {
            // Before the corresponding PointerFlags have been returned, the ReferenceClassifier instance has not been built from the factory.
            Assert.Throws<NullReferenceException>(() => m_typeSystem!.GetTagAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 2));

            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetPointerOffsets((int)TestTypeIndex.ReferenceClassifiers, baseOffset: 0).ToList();
            Assert.That(pointerInfos, Has.Count.GreaterThan(2));
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[2].PointerFlags, Is.EqualTo(PointerFlags.IsTagAnchor));
                Assert.That(pointerInfos[2].FieldNumber, Is.EqualTo(2));
            });

            List<(Selector selector, List<string> tags, string location)> result = m_typeSystem.GetTagAnchorSelectors((int)TestTypeIndex.ReferenceClassifiers, 2).ToList();
            Assert.That(result, Has.Exactly(1).Items);
            Assert.Multiple(() =>
            {
                Assert.That(result[0].selector.StaticPrefix, Has.Exactly(1).Items);
                Assert.That(result[0].tags, Has.Exactly(2).Items);
            });
        }

        [Test]
        public void TestGetTags()
        {
            // Before the corresponding PointerFlags have been returned, the ReferenceClassifier instance has not been built from the factory.
            Assert.Throws<NullReferenceException>(() => m_typeSystem!.GetTags((int)TestTypeIndex.ReferenceClassifiers, 3));

            List<PointerInfo<int>> pointerInfos = m_typeSystem!.GetPointerOffsets((int)TestTypeIndex.ReferenceClassifiers, baseOffset: 0).ToList();
            Assert.That(pointerInfos, Has.Count.GreaterThan(3));
            Assert.Multiple(() =>
            {
                Assert.That(pointerInfos[3].PointerFlags, Is.EqualTo(PointerFlags.TagIfZero));
                Assert.That(pointerInfos[3].FieldNumber, Is.EqualTo(3));
            });

            (List<string> zeroTags, List<string> nonZeroTags) = m_typeSystem.GetTags((int)TestTypeIndex.ReferenceClassifiers, 3);
            Assert.Multiple(() =>
            {
                Assert.That(zeroTags, Has.Exactly(2).Items);
                Assert.That(nonZeroTags, Has.Exactly(1).Items);
            });
        }
    }
}
