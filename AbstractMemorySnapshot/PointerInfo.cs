// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    [Flags]
    public enum PointerFlags
    {
        None = 0,
        IsOwningReference = 1 << 0,
        IsConditionAnchor = 1 << 1,
        IsWeakReference = 1 << 2,
        Untraced = 1 << 3,
        IsExternalReference = 1 << 4,
        TagIfZero = 1 << 5,
        TagIfNonZero = 1 << 6,
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
