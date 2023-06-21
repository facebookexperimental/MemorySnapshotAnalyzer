// Copyright(c) Meta Platforms, Inc. and affiliates.

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public enum PointerFlags
    {
        None = 0,
        IsOwningReference = 1 << 0,
        IsConditionAnchor = 1 << 1
    }

    public struct PointerInfo<T>
    {
        public T Value;
        public PointerFlags PointerFlags;
        public int TypeIndex;
        public int FieldNumber;

        public PointerInfo<U> WithValue<U>(U value)
        {
            return new PointerInfo<U>
            {
                Value = value,
                PointerFlags = PointerFlags,
                TypeIndex = TypeIndex,
                FieldNumber = FieldNumber
            };
        }
    }
}
