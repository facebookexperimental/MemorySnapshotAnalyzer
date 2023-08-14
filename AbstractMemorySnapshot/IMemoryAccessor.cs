/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public interface IMemoryAccessor
    {
        void CheckRange(long offset, long size);

        MemoryView GetRange(long offset, long size);

        void Read<T>(long position, out T sructure) where T : struct;
    }
}
