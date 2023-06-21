// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public class ReferenceClassifierFactory
    {
        public class ReferenceClassifier
        {
            readonly List<(int typeIndex, int fieldNumber)[]> m_emptyList;

            public ReferenceClassifier()
            {
                m_emptyList = new List<(int typeIndex, int fieldNumber)[]>();
            }

            public virtual bool IsOwningReference(int typeIndex, int fieldNumber)
            {
                return false;
            }

            public virtual bool IsConditionAnchor(int typeIndex, int fieldNumber)
            {
                return false;
            }

            public virtual List<(int typeIndex, int fieldNumber)[]> GetConditionalAnchorFieldPaths(int typeIndex, int fieldNumber)
            {
                return m_emptyList;
            }
        }

        public virtual ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new ReferenceClassifier();
        }

        public virtual string Description => "not set";
    }
}
