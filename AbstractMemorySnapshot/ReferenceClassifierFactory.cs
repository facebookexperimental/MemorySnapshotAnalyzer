/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public struct Selector
    {
        public List<(int typeIndex, int fieldNumber)> StaticPrefix;
        public string[]? DynamicTail;
    }

    public abstract class ReferenceClassifier
    {
        protected ReferenceClassifier() { }

        public abstract PointerFlags GetPointerFlags(int typeIndex, int fieldNumber);

        public abstract IEnumerable<Selector> GetConditionAnchorSelectors(int typeIndex, int fieldNumber);

        public abstract IEnumerable<(Selector selector, List<string> tags)> GetTagAnchorSelectors(int typeIndex, int fieldNumber);

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
            return PointerFlags.None;
        }

        public override IEnumerable<Selector> GetConditionAnchorSelectors(int typeIndex, int fieldNumber)
        {
            yield break;
        }

        public override IEnumerable<(Selector selector, List<string> tags)> GetTagAnchorSelectors(int typeIndex, int fieldNumber)
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
