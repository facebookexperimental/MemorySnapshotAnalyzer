// Copyright(c) Meta Platforms, Inc. and affiliates.

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
        }

        public virtual ReferenceClassifier Build(TypeSystem typeSystem)
        {
            return new ReferenceClassifier();
        }

        public virtual string Description => "not set";
    }
}
