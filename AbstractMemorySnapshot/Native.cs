/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public readonly struct Native
    {
        readonly int m_size;

        public Native(int size)
        {
            m_size = size;
        }

        public int Size => m_size;

        public NativeWord Zero => new(m_size, 0);

        public NativeWord From(int i)
        {
            return new NativeWord(m_size, (ulong)i);
        }

        public NativeWord From(long i)
        {
            return new NativeWord(m_size, (ulong)i);
        }

        public NativeWord From(ulong i)
        {
            return new NativeWord(m_size, i);
        }
    }
}
