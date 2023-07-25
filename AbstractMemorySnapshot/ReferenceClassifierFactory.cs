// Copyright(c) Meta Platforms, Inc. and affiliates.

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

        public abstract IEnumerable<(Selector selector, string tag)> GetTagAnchorSelectors(int typeIndex, int fieldNumber);

        public abstract (string? zeroTag, string? nonZeroTag) GetTags(int typeIndex, int fieldNumber);
    }

    public abstract class ReferenceClassifierFactory
    {
        public abstract ReferenceClassifier Build(TypeSystem typeSystem);
    }

    public class DefaultReferenceClassifier : ReferenceClassifier
    {
        public DefaultReferenceClassifier() { }

        public override PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
        {
            return PointerFlags.None;
        }

        public override IEnumerable<Selector> GetConditionAnchorSelectors(int typeIndex, int fieldNumber)
        {
            yield break;
        }

        public override IEnumerable<(Selector selector, string tag)> GetTagAnchorSelectors(int typeIndex, int fieldNumber)
        {
            yield break;
        }

        public override (string? zeroTag, string? nonZeroTag) GetTags(int typeIndex, int fieldNumber)
        {
            return (zeroTag: null, nonZeroTag: null);
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
