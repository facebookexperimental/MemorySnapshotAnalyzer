/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public struct Selector
    {
        // Sentinel value for array indexing (all elements)
        public static readonly int FieldNumberArraySentinel = Int32.MaxValue;

        public List<(int typeIndex, int fieldNumber)> StaticPrefix;
        public string[]? DynamicTail;

        public readonly string Stringify(TypeSystem typeSystem, int pathIndex, bool inStaticPrefix)
        {
            StringBuilder sb = new();
            if (StaticPrefix != null)
            {
                for (int i = 0; i < StaticPrefix.Count; i++)
                {
                    (int typeIndex, int fieldNumber) = StaticPrefix[i];
                    if (sb.Length > 0 && fieldNumber != FieldNumberArraySentinel)
                    {
                        sb.Append('.');
                    }

                    if (inStaticPrefix && pathIndex == i)
                    {
                        sb.Append('^');
                    }
                    sb.Append(fieldNumber == FieldNumberArraySentinel ? "[]" : typeSystem.FieldName(typeIndex, fieldNumber));
                }
            }

            if (DynamicTail != null)
            {
                for (int i = 0; i < DynamicTail.Length; i++)
                {
                    string fieldName = DynamicTail[i];
                    if (sb.Length > 0 && !fieldName.Equals("[]", StringComparison.Ordinal))
                    {
                        sb.Append('.');
                    }

                    if (!inStaticPrefix && pathIndex == i)
                    {
                        sb.Append('^');
                    }
                    sb.Append(fieldName);
                }
            }

            return sb.ToString();
        }
    }

    public abstract class ReferenceClassifier
    {
        protected ReferenceClassifier() { }

        public abstract PointerFlags GetPointerFlags(int typeIndex, int fieldNumber);

        public abstract IEnumerable<(Selector selector, int weight, string location)> GetWeightAnchorSelectors(int typeIndex, int fieldNumber);

        public abstract IEnumerable<(Selector selector, List<string> tags, string location)> GetTagAnchorSelectors(int typeIndex, int fieldNumber);

        public abstract (List<string> zeroTags, List<string> nonZeroTags) GetTags(int typeIndex, int fieldNumber);
    }

    public abstract class ReferenceClassifierFactory
    {
        public abstract ReferenceClassifier Build(TypeSystem typeSystem);
    }

    public class DefaultReferenceClassifier : ReferenceClassifier
    {
        static readonly List<string> s_emptyList = new();

        public DefaultReferenceClassifier() { }

        public override PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
        {
            return default;
        }

        public override IEnumerable<(Selector selector, int weight, string location)> GetWeightAnchorSelectors(int typeIndex, int fieldNumber)
        {
            yield break;
        }

        public override IEnumerable<(Selector selector, List<string> tags, string location)> GetTagAnchorSelectors(int typeIndex, int fieldNumber)
        {
            yield break;
        }

        public override (List<string> zeroTags, List<string> nonZeroTags) GetTags(int typeIndex, int fieldNumber)
        {
            return (zeroTags: s_emptyList, nonZeroTags: s_emptyList);
        }
    }

    public class DefaultReferenceClassifierFactory : ReferenceClassifierFactory
    {
        public override ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new DefaultReferenceClassifier();
        }
    }
}
