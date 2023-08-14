/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

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
        IsTagAnchor = 1 << 5,
        TagIfZero = 1 << 6,
        TagIfNonZero = 1 << 7,
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
