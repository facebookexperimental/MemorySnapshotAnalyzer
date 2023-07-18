// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class ReferenceClassifier
    {
        protected ReferenceClassifier() { }

        public abstract PointerFlags GetPointerFlags(int typeIndex, int fieldNumber);

        public abstract List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber);

        public abstract (string? zeroTag, string? nonZeroTag) GetTags(int typeIndex, int fieldNumber);
    }

    public abstract class ReferenceClassifierFactory
    {
        public abstract ReferenceClassifier Build(TypeSystem typeSystem);
    }

    public class DefaultReferenceClassifier : ReferenceClassifier
    {
        readonly List<(int typeIndex, int fieldNumber)[]> m_emptyList;

        public DefaultReferenceClassifier()
        {
            m_emptyList = new List<(int typeIndex, int fieldNumber)[]>();
        }

        public override PointerFlags GetPointerFlags(int typeIndex, int fieldNumber)
        {
            return PointerFlags.None;
        }

        public override List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
        {
            return m_emptyList;
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
