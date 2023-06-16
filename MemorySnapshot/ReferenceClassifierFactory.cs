// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public class ReferenceClassifierFactory
    {
        public class ReferenceClassifier
        {
            public virtual bool IsOwningReference(int typeIndex, int fieldNumber)
            {
                return false;
            }

            public virtual bool IsConditionalOwningReference(int typeIndex, int fieldNumber)
            {
                return false;
            }

            public virtual bool CheckConditionalOwningReference(int typeIndex, int fieldNumber, Func<int, int> getAncestorTypeIndex, Func<int, bool> testField)
            {
                return false;
            }
        }

        public virtual ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new ReferenceClassifier();
        }

        public virtual string Description => "not set";
    }
}
