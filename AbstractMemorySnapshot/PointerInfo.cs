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
    public enum PointerFlags : ulong
    {
        // Weight of this reference (the strength of the bond, so to speak)
        // is stored in the upper 32 bits:
        // - Weight > 0: strongly-owning reference
        // - Weight == 0: regular reference (no OWNS rule applied to this reference)
        // - Weight < 0: weakly-owning reference
        // Note that the values below are chosen such that default(PointerFlags) is a regular reference.
        Weighted = 0,
        IsWeightAnchor = 1 << 0,
        Untraced = 1 << 1,
        IsExternalReference = 1 << 2,
        IsTagAnchor = 1 << 3,
        TagIfZero = 1 << 4,
        TagIfNonZero = 1 << 5,
    }

    public static class PointerFlagsExtensions
    {
        public static int Weight(this PointerFlags pointerFlags)
        {
            return (int)((ulong)pointerFlags >> 32);
        }

        public static PointerFlags WithoutWeight(this PointerFlags pointerFlags)
        {
            return pointerFlags & (PointerFlags)0xFFFFFFFFul;
        }

        public static PointerFlags WithWeight(this PointerFlags pointerFlags, int weight)
        {
            return pointerFlags.WithoutWeight() | (PointerFlags)((ulong)weight << 32);
        }

        public static PointerFlags CombineWith(this PointerFlags pointerFlags, PointerFlags other)
        {
            PointerFlags baseFlags = (pointerFlags | other).WithoutWeight();
            return baseFlags.WithWeight(Math.Max(pointerFlags.Weight(), other.Weight()));
        }
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
