﻿// Copyright(c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public abstract class ReferenceClassifier
    {
        protected ReferenceClassifier() { }

        public abstract bool IsOwningReference(int typeIndex, int fieldNumber);

        public abstract bool IsConditionAnchor(int typeIndex, int fieldNumber);

        public abstract List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber);
    }

    public abstract class ReferenceClassifierFactory
    {
        public abstract ReferenceClassifier Build(TypeSystem typeSystem);

        public abstract string Description { get; }
    }

    public class DefaultReferenceClassifier : ReferenceClassifier
    {
        readonly List<(int typeIndex, int fieldNumber)[]> m_emptyList;

        public DefaultReferenceClassifier()
        {
            m_emptyList = new List<(int typeIndex, int fieldNumber)[]>();
        }

        public override bool IsOwningReference(int typeIndex, int fieldNumber)
        {
            return false;
        }

        public override bool IsConditionAnchor(int typeIndex, int fieldNumber)
        {
            return false;
        }

        public override List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
        {
            return m_emptyList;
        }
    }

    public class DefaultReferenceClassifierFactory : ReferenceClassifierFactory
    {
        public override ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new DefaultReferenceClassifier();
        }

        public override string Description => "not set";
    }
}
