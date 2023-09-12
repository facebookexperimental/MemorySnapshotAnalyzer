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
using System.Text.RegularExpressions;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshotTests
{
    public enum TestTypeIndex : int
    {
        None = -1,

        Primitive = 0,
        ObjectNoPointers,
        ValueTypeTwoPointers,
        ObjectTwoPointers,
        ObjectTwoPointersInValueType,
        DerivedTypeFourPointers,
        ClassWithStaticFields,
        FieldWithPointerFlagsExternal,
        ObjectArray,
        ValueTypeArray,
        ReferenceClassifiers,
        DerivedFromReferenceClassifier,
        GenericTypeWithNesting,
        GenericTypeArray,
        EmptyTypeNameCornerCase,
        WeightedReferences,

        Configurable,

        NumberOfTypeIndices,
    }

    public sealed class TestTypeSystem : TypeSystem
    {
        sealed class FieldInfo
        {
            internal readonly string Name;
            internal readonly int Offset;
            internal readonly int FieldTypeIndex;
            internal readonly bool IsStatic;

            internal FieldInfo(string name, int offset, TestTypeIndex fieldTypeIndex, bool isStatic = false)
            {
                Name = name;
                Offset = offset;
                FieldTypeIndex = (int)fieldTypeIndex;
                IsStatic = isStatic;
            }
        }

        sealed class TypeInfo
        {
            internal readonly string Name;
            internal readonly int BaseOrElementTypeIndex;
            internal readonly int BaseSize;
            internal readonly List<FieldInfo> FieldInfos;
            internal readonly bool IsValueType;
            internal readonly bool IsArray;

            internal TypeInfo(string name, int baseSize, int baseOrElementTypeIndex, List<FieldInfo> fieldInfos, bool isValueType, bool isArray)
            {
                Name = name;
                BaseOrElementTypeIndex = baseOrElementTypeIndex;
                BaseSize = baseSize;
                FieldInfos = fieldInfos;
                IsValueType = isValueType;
                IsArray = isArray;
            }

            static internal TypeInfo ForValueType(string name, int baseSize, List<FieldInfo> fieldInfos)
            {
                return new TypeInfo(name, baseSize, -1, fieldInfos, isValueType: true, isArray: false);
            }

            static internal TypeInfo ForObject(string name, int baseSize, TestTypeIndex baseTypeIndex, List<FieldInfo> fieldInfos)
            {
                return new TypeInfo(name, baseSize, (int)baseTypeIndex, fieldInfos, isValueType: false, isArray: false);
            }

            static internal TypeInfo ForArray(string name, TestTypeIndex elementTypeIndex)
            {
                return new TypeInfo(name, 32, (int)elementTypeIndex, new List<FieldInfo>(), isValueType: false, isArray: true);
            }
        }

        sealed class MockReferenceClassifierFactory : ReferenceClassifierFactory
        {
            sealed class MockReferenceClassifier : ReferenceClassifier
            {
                public override PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
                {
                    if ((TestTypeIndex)typeIndex == TestTypeIndex.DerivedTypeFourPointers && fieldNumber == 0)
                    {
                        return PointerFlags.Weighted.WithWeight(1);
                    }
                    else if ((TestTypeIndex)typeIndex == TestTypeIndex.FieldWithPointerFlagsExternal && fieldNumber == 0)
                    {
                        return PointerFlags.IsExternalReference;
                    }
                    else if ((TestTypeIndex)typeIndex == TestTypeIndex.ReferenceClassifiers)
                    {
                        switch (fieldNumber)
                        {
                            case 0:
                            case 1:
                                return PointerFlags.IsWeightAnchor;
                            case 2:
                                return PointerFlags.IsTagAnchor;
                            case 3:
                                return PointerFlags.TagIfZero;
                        }
                    }
                    else if ((TestTypeIndex)typeIndex == TestTypeIndex.WeightedReferences)
                    {
                        switch (fieldNumber)
                        {
                            case 0:
                                return default;
                            case 1:
                                return PointerFlags.Weighted.WithWeight(1);
                            case 2:
                                return PointerFlags.Weighted.WithWeight(-1);
                        }
                    }
                    return default;
                }

                public override IEnumerable<(Selector selector, int weight, string location)> GetWeightAnchorSelectors(int typeIndex, int fieldNumber)
                {
                    if ((TestTypeIndex)typeIndex != TestTypeIndex.ReferenceClassifiers || (fieldNumber != 0 && fieldNumber != 1))
                    {
                        Assert.Fail("GetWeightAnchorSelectors invoked for invalid field");
                    }

                    yield return (new()
                    {
                        StaticPrefix = new() { ((int)TestTypeIndex.ReferenceClassifiers, 0) },
                        DynamicTail = fieldNumber == 0 ? null : new string[] { "derivedField" },
                    }, weight: fieldNumber == 1 ? 3 : 0, location: $"{typeIndex}:{fieldNumber}");
                }

                public override IEnumerable<(Selector selector, List<string> tags, string location)> GetTagAnchorSelectors(int typeIndex, int fieldNumber)
                {
                    if ((TestTypeIndex)typeIndex != TestTypeIndex.ReferenceClassifiers || fieldNumber != 2)
                    {
                        Assert.Fail("GetTagAnchorSelectors invoked for invalid field");
                    }

                    Selector selector = new()
                    {
                        StaticPrefix = new() { ((int)TestTypeIndex.ReferenceClassifiers, 0) },
                        DynamicTail = new string[] { "derivedField" },
                    };
                    List<string> tags = new() { "tag1", "tag2" };
                    yield return (selector, tags, location: $"{typeIndex}:{fieldNumber}");
                }

                public override (List<string> zeroTags, List<string> nonZeroTags) GetTags(int typeIndex, int fieldNumber)
                {
                    if ((TestTypeIndex)typeIndex != TestTypeIndex.ReferenceClassifiers || fieldNumber != 3)
                    {
                        Assert.Fail("GetTags invoked for invalid field");
                    }

                    return (new() { "zeroTag1", "zeroTag2" }, new() { "nonZeroTag" });
                }
            }

            public override ReferenceClassifier Build(TypeSystem typeSystem)
            {
                return new MockReferenceClassifier();
            }
        }

        readonly Dictionary<TestTypeIndex, TypeInfo> m_typeInfos;
        int m_configurableTypeIndex;

        public TestTypeSystem() : base(new MockReferenceClassifierFactory())
        {
            m_typeInfos = new();
            m_typeInfos.Add(TestTypeIndex.Primitive,
                TypeInfo.ForValueType("System.Int64", 8, new()
                {
                    new("value", 16, TestTypeIndex.Primitive),
                }));
            m_typeInfos.Add(TestTypeIndex.ObjectNoPointers,
                TypeInfo.ForObject("ObjectNoPointers", 24, TestTypeIndex.None, new()
                {
                    new("primitive", 16, TestTypeIndex.Primitive),
                }));
            m_typeInfos.Add(TestTypeIndex.ValueTypeTwoPointers,
                TypeInfo.ForValueType("ValueTypeTwoPointers", 32, new()
                {
                    new("object1", 16, TestTypeIndex.ObjectNoPointers),
                    new("object2", 24, TestTypeIndex.ObjectTwoPointers),
                }));
            m_typeInfos.Add(TestTypeIndex.ObjectTwoPointers,
                TypeInfo.ForObject("ObjectTwoPointers", 32, TestTypeIndex.None, new()
                {
                    new("object1", 16, TestTypeIndex.ObjectNoPointers),
                    new("object2", 24, TestTypeIndex.ObjectNoPointers),
                }));
            m_typeInfos.Add(TestTypeIndex.ObjectTwoPointersInValueType,
                TypeInfo.ForObject("ObjectTwoPointersInValueType", 40, TestTypeIndex.None, new()
                {
                    new("value", 16, TestTypeIndex.ValueTypeTwoPointers),
                    new("object", 32, TestTypeIndex.ObjectTwoPointersInValueType),
                }));
            m_typeInfos.Add(TestTypeIndex.DerivedTypeFourPointers,
                TypeInfo.ForObject("DerivedTypeFourPointers", 48, TestTypeIndex.ObjectTwoPointers, new()
                {
                    new("object3", 32, TestTypeIndex.ObjectTwoPointers), // IsOwningReference
                    new("next", 40, TestTypeIndex.DerivedTypeFourPointers),
                }));
            m_typeInfos.Add(TestTypeIndex.ClassWithStaticFields,
                TypeInfo.ForObject("ClassWithStaticFields", 40, TestTypeIndex.None, new()
                {
                    new("object1", 16, TestTypeIndex.ValueTypeTwoPointers),
                    new("static1", 0, TestTypeIndex.ObjectNoPointers, isStatic: true),
                    new("object2", 32, TestTypeIndex.ObjectNoPointers),
                    new("static2", 8, TestTypeIndex.ValueTypeTwoPointers, isStatic: true),
                }));
            m_typeInfos.Add(TestTypeIndex.FieldWithPointerFlagsExternal,
                TypeInfo.ForObject("FieldWithPointerFlagsExternal", 32, TestTypeIndex.None, new()
                {
                    new("object1", 16, TestTypeIndex.Primitive), // IsExternalReference
                    new("object2", 24, TestTypeIndex.ObjectNoPointers),
                }));
            m_typeInfos.Add(TestTypeIndex.ObjectArray,
                TypeInfo.ForArray("ObjectNoPointers[]", TestTypeIndex.ObjectNoPointers));
            m_typeInfos.Add(TestTypeIndex.ValueTypeArray,
                TypeInfo.ForArray("ValueTypeTwoPointers[]", TestTypeIndex.ValueTypeTwoPointers));
            m_typeInfos.Add(TestTypeIndex.ReferenceClassifiers,
                TypeInfo.ForObject("ReferenceClassifiers", 72, TestTypeIndex.None, new()
                {
                    new("weightAnchorStatic", 16, TestTypeIndex.ObjectTwoPointers), // IsWeightAnchhor
                    new("weightAnchorDynamic", 24, TestTypeIndex.ObjectTwoPointers), // IsWeightAnchor
                    new("tagAnchor", 32, TestTypeIndex.ObjectTwoPointers), // IsTagAnchor
                    new("tagIfZero", 40, TestTypeIndex.ObjectTwoPointers), // TagIfZero
                    new("object", 48, TestTypeIndex.ReferenceClassifiers),
                    new("array", 56, TestTypeIndex.ObjectArray),
                    new("valueTypeArray", 64, TestTypeIndex.ValueTypeArray),
                }));
            m_typeInfos.Add(TestTypeIndex.DerivedFromReferenceClassifier,
                TypeInfo.ForObject("ReferenceClassifiers", 64, TestTypeIndex.ReferenceClassifiers, new()
                {
                    new("derivedField", 56, TestTypeIndex.ReferenceClassifiers),
                }));
            m_typeInfos.Add(TestTypeIndex.GenericTypeWithNesting,
                TypeInfo.ForObject("GenericTypeWithNesting<int[], Dictionary<Foo, Bar>>", 32, TestTypeIndex.None, new() { }));
            m_typeInfos.Add(TestTypeIndex.GenericTypeArray,
                TypeInfo.ForObject("GenericTypeArray<int>[]", 32, TestTypeIndex.None, new() { }));
            m_typeInfos.Add(TestTypeIndex.EmptyTypeNameCornerCase,
                TypeInfo.ForObject("", 32, TestTypeIndex.None, new() { }));
            m_typeInfos.Add(TestTypeIndex.WeightedReferences,
                TypeInfo.ForObject("WeightedReferences", 40, TestTypeIndex.None, new()
                {
                    new("regular", 16, TestTypeIndex.ObjectNoPointers),
                    new("strong", 24, TestTypeIndex.ObjectNoPointers),
                    new("weak", 32, TestTypeIndex.ObjectNoPointers),
                }));
        }

        public void SetTargetForConfigurableTypeIndex(TestTypeIndex targetTypeIndex)
        {
            m_configurableTypeIndex = (int)targetTypeIndex;
        }

        public override int PointerSize => 8;

        public override int NumberOfTypeIndices => (int)TestTypeIndex.NumberOfTypeIndices;

        public override string Assembly(int typeIndex)
        {
            return "Test.Assembly";
        }

        public override string QualifiedName(int typeIndex)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].Name :
                QualifiedName(m_configurableTypeIndex);
        }

        static readonly Regex s_identifierAndDotRegex = new("[a-zA-Z_][a-zA-Z0-9_]*\\.", RegexOptions.Compiled);

        public override string UnqualifiedName(int typeIndex)
        {
            string qualifiedName = QualifiedName(typeIndex);
            return s_identifierAndDotRegex.Replace(qualifiedName, string.Empty);
        }

        public override int BaseOrElementTypeIndex(int typeIndex)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].BaseOrElementTypeIndex :
                BaseOrElementTypeIndex(m_configurableTypeIndex);
        }

        public override int ObjectHeaderSize(int typeIndex)
        {
            return 16;
        }

        public override int BaseSize(int typeIndex)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].BaseSize :
                BaseSize(m_configurableTypeIndex);
        }

        public override bool IsValueType(int typeIndex)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].IsValueType :
                IsValueType(m_configurableTypeIndex);
        }

        public override bool IsArray(int typeIndex)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].IsArray :
                IsArray(m_configurableTypeIndex);
        }

        public override int Rank(int typeIndex)
        {
            throw new NotImplementedException();
        }

        public override int NumberOfFields(int typeIndex)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].FieldInfos.Count :
                NumberOfFields(m_configurableTypeIndex);
        }

        public override int FieldOffset(int typeIndex, int fieldNumber, bool withHeader)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                (m_typeInfos[(TestTypeIndex)typeIndex].FieldInfos[fieldNumber].Offset - (withHeader ? 0 : 16)) :
                FieldOffset(m_configurableTypeIndex, fieldNumber, withHeader);
        }

        public override int FieldType(int typeIndex, int fieldNumber)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].FieldInfos[fieldNumber].FieldTypeIndex :
                FieldType(m_configurableTypeIndex, fieldNumber);
        }

        public override string FieldName(int typeIndex, int fieldNumber)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].FieldInfos[fieldNumber].Name :
                FieldName(m_configurableTypeIndex, fieldNumber);
        }

        public override bool FieldIsStatic(int typeIndex, int fieldNumber)
        {
            return typeIndex != (int)TestTypeIndex.Configurable ?
                m_typeInfos[(TestTypeIndex)typeIndex].FieldInfos[fieldNumber].IsStatic :
                FieldIsStatic(m_configurableTypeIndex, fieldNumber);
        }

        public override MemoryView StaticFieldBytes(int typeIndex, int fieldNumber)
        {
            // "default" is an invalid MemoryView, which is returned when the initializer hasn't been run yet.
            return default;
        }

        public override int GetArrayElementOffset(int elementTypeIndex, int elementIndex)
        {
            return 32 + elementIndex * GetArrayElementSize(elementTypeIndex);
        }

        public override int GetArrayElementSize(int elementTypeIndex)
        {
            if (IsValueType(elementTypeIndex))
            {
                return BaseSize(elementTypeIndex);
            }
            else
            {
                return PointerSize;
            }
        }

        public override int SystemStringTypeIndex => throw new NotImplementedException();

        public override int SystemStringLengthOffset => throw new NotImplementedException();

        public override int SystemStringFirstCharOffset => throw new NotImplementedException();

        public override int SystemVoidStarTypeIndex => throw new NotImplementedException();

        public override IEnumerable<string> DumpStats()
        {
            throw new NotImplementedException();
        }
    }
}
