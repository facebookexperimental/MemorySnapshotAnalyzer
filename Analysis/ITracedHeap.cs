// Copyright(c) Meta Platforms, Inc. and affiliates.

using MemorySnapshotAnalyzer.AbstractMemorySnapshot;
using System.Collections.Generic;
using System;

namespace MemorySnapshotAnalyzer.Analysis
{
    public interface ITracedHeap
    {
        IRootSet RootSet { get; }

        int NumberOfLiveObjects { get; }

        int NumberOfInvalidRoots { get; }

        int NumberOfInvalidPointers { get; }

        IEnumerable<Tuple<int, NativeWord>> GetInvalidRoots();

        IEnumerable<Tuple<NativeWord, NativeWord>> GetInvalidPointers();

        int GetNumberOfPredecessors(int objectIndex);

        int ObjectAddressToIndex(NativeWord address);

        // Note that this returns objects in postorder.
        NativeWord ObjectAddress(int objectIndex);

        int ObjectTypeIndex(int objectIndex);
    }
}
